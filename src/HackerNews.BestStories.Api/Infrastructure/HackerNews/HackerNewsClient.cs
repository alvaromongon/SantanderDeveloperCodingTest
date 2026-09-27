namespace HackerNews.BestStories.Api.Infrastructure.HackerNews;

/// <summary>
/// Typed <see cref="HttpClient"/> for the Hacker News API. Resilience and the base address are
/// configured when it is registered.
/// </summary>
internal sealed class HackerNewsClient(HttpClient httpClient) : IHackerNewsClient
{
    public async Task<IReadOnlyList<long>> GetBestStoryIdsAsync(CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync(
            "beststories.json", HackerNewsJsonContext.Default.Int64Array, cancellationToken) ?? [];

    public Task<HackerNewsItem?> GetItemAsync(long id, CancellationToken cancellationToken) =>
        httpClient.GetFromJsonAsync(
            $"item/{id}.json", HackerNewsJsonContext.Default.HackerNewsItem, cancellationToken);
}
