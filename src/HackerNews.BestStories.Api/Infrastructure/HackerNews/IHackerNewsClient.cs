namespace HackerNews.BestStories.Api.Infrastructure.HackerNews;

/// <summary>
/// Client for the Hacker News API (<see href="https://github.com/HackerNews/API"/>).
/// </summary>
internal interface IHackerNewsClient
{
    /// <summary>
    /// Gets the IDs of the best stories (up to 200), in the order returned by Hacker News.
    /// </summary>
    /// <exception cref="HttpRequestException">Hacker News returned an error status code.</exception>
    Task<IReadOnlyList<long>> GetBestStoryIdsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets an item by its ID.
    /// </summary>
    /// <returns>The item, or <see langword="null"/> when it does not exist.</returns>
    /// <exception cref="HttpRequestException">Hacker News returned an error status code.</exception>
    Task<HackerNewsItem?> GetItemAsync(long id, CancellationToken cancellationToken);
}
