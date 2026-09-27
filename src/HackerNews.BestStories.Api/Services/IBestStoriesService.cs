using HackerNews.BestStories.Api.Models;

namespace HackerNews.BestStories.Api.Services;

/// <summary>
/// Provides the best Hacker News stories ranked by score, served from a cache so incoming traffic
/// does not reach the Hacker News API.
/// </summary>
internal interface IBestStoriesService
{
    /// <summary>
    /// Gets the best <paramref name="count"/> stories ordered by score descending; ties keep the
    /// Hacker News order. Returns every available story when <paramref name="count"/> exceeds them.
    /// On a cold cache, concurrent callers share a single rebuild of the ranking.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than 1.</exception>
    /// <exception cref="HackerNewsUnavailableException">The cache is cold and Hacker News failed.</exception>
    Task<IReadOnlyList<StoryResponse>> GetBestStoriesAsync(int count, CancellationToken cancellationToken);

    /// <summary>
    /// Re-fetches the best stories from Hacker News and overwrites the cached ranking. Items that
    /// fail keep their cached copy; the ranking is only overwritten when the refresh succeeds.
    /// </summary>
    /// <exception cref="HackerNewsUnavailableException">Hacker News failed; the last ranking is kept.</exception>
    Task RefreshAsync(CancellationToken cancellationToken);
}
