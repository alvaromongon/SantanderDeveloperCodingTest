using System.Collections.Immutable;
using System.Text.Json;

using HackerNews.BestStories.Api.Infrastructure.HackerNews;
using HackerNews.BestStories.Api.Models;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

using Polly;

namespace HackerNews.BestStories.Api.Services;

/// <summary>
/// Ranks the Hacker News best stories by score and caches the IDs, each item and the ranking in
/// <see cref="HybridCache"/>. Requests only read the ranking; the calls to Hacker News are bounded by
/// the concurrency limiter of the <see cref="IHackerNewsClient"/> resilience pipeline.
/// </summary>
internal sealed partial class BestStoriesService(
    IHackerNewsClient client,
    HybridCache cache,
    IOptions<HackerNewsOptions> options,
    ILogger<BestStoriesService> logger) : IBestStoriesService
{
    private const string BestStoryIdsKey = "hackernews:beststories";
    private const string RankedStoriesKey = "beststories:ranked";

    // Reads a cached entry without calling Hacker News or writing to the cache.
    private static readonly HybridCacheEntryOptions CachedOnly = new()
    {
        Flags = HybridCacheEntryFlags.DisableUnderlyingData | HybridCacheEntryFlags.DisableLocalCacheWrite
            | HybridCacheEntryFlags.DisableDistributedCacheWrite,
    };

    private readonly HybridCacheEntryOptions _entryOptions = new()
    {
        Expiration = options.Value.CacheExpiration,
        LocalCacheExpiration = options.Value.CacheExpiration,
    };

    public async Task<IReadOnlyList<StoryResponse>> GetBestStoriesAsync(int count, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var ranking = await cache.GetOrCreateAsync(
            RankedStoriesKey,
            this,
            static (service, token) => service.RankAsync(refresh: false, token),
            _entryOptions,
            cancellationToken: cancellationToken);

        return ranking.Take(count);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var ranking = await RankAsync(refresh: true, cancellationToken);
        await cache.SetAsync(RankedStoriesKey, ranking, _entryOptions, cancellationToken: cancellationToken);
    }

    // refresh: re-fetch everything from Hacker News instead of fetching only what is not cached.
    private async ValueTask<RankedStories> RankAsync(bool refresh, CancellationToken cancellationToken)
    {
        var ids = await GetBestStoryIdsAsync(refresh, cancellationToken);
        var results = await Task.WhenAll(ids.Select(id => GetStoryAsync(id, refresh, cancellationToken)));

        var stories = results
            .Select(result => result.Story)
            .OfType<StoryResponse>()
            .OrderByDescending(story => story.Score) // Stable: ties keep the Hacker News order.
            .ToImmutableArray();

        if (stories.IsEmpty && results.Any(result => result.Failed))
        {
            throw new HackerNewsUnavailableException("No Hacker News best story could be retrieved.");
        }

        return new RankedStories(stories);
    }

    private async Task<IReadOnlyList<long>> GetBestStoryIdsAsync(bool refresh, CancellationToken cancellationToken)
    {
        try
        {
            if (!refresh)
            {
                return await cache.GetOrCreateAsync(
                    BestStoryIdsKey,
                    client,
                    static async (client, token) => await client.GetBestStoryIdsAsync(token),
                    _entryOptions,
                    cancellationToken: cancellationToken);
            }

            var ids = await client.GetBestStoryIdsAsync(cancellationToken);
            await cache.SetAsync(BestStoryIdsKey, ids, _entryOptions, cancellationToken: cancellationToken);
            return ids;
        }
        catch (Exception exception) when (IsUpstreamFailure(exception))
        {
            throw new HackerNewsUnavailableException("The Hacker News best story IDs could not be retrieved.", exception);
        }
    }

    // A failed item is skipped (or keeps its cached copy when refreshing) instead of failing the ranking.
    private async Task<StoryResult> GetStoryAsync(long id, bool refresh, CancellationToken cancellationToken)
    {
        var key = $"hackernews:item:{id}";

        try
        {
            if (!refresh)
            {
                return new StoryResult(await cache.GetOrCreateAsync(
                    key,
                    (client, id),
                    static (state, token) => FetchStoryAsync(state.client, state.id, token),
                    _entryOptions,
                    cancellationToken: cancellationToken));
            }

            var story = await FetchStoryAsync(client, id, cancellationToken);
            await cache.SetAsync(key, story, _entryOptions, cancellationToken: cancellationToken);
            return new StoryResult(story);
        }
        catch (Exception exception) when (IsUpstreamFailure(exception))
        {
            LogItemFailed(logger, id, exception);

            var cachedStory = refresh
                ? await cache.GetOrCreateAsync(
                    key, static _ => ValueTask.FromResult<StoryResponse?>(null), CachedOnly, cancellationToken: cancellationToken)
                : null;

            return new StoryResult(cachedStory, Failed: cachedStory is null);
        }
    }

    private static async ValueTask<StoryResponse?> FetchStoryAsync(
        IHackerNewsClient client, long id, CancellationToken cancellationToken) =>
        (await client.GetItemAsync(id, cancellationToken))?.ToStoryResponse();

    // Error status, malformed payload, or a call rejected by the resilience pipeline (timeout,
    // open circuit, concurrency queue full). Cancellation is not a failure and propagates.
    private static bool IsUpstreamFailure(Exception exception) =>
        exception is HttpRequestException or JsonException or ExecutionRejectedException;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Hacker News item {ItemId} could not be retrieved.")]
    private static partial void LogItemFailed(ILogger logger, long itemId, Exception exception);

    private readonly record struct StoryResult(StoryResponse? Story, bool Failed = false);
}
