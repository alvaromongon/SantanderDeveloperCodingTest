namespace HackerNews.BestStories.Api.Infrastructure.HackerNews;

/// <summary>
/// Item returned by the Hacker News API (<c>item/{id}.json</c>).
/// Only the fields used by this service are mapped.
/// </summary>
internal sealed record HackerNewsItem
{
    public long Id { get; init; }

    public string? Type { get; init; }

    public string? By { get; init; }

    /// <summary>Creation time in Unix seconds.</summary>
    public long Time { get; init; }

    public string? Title { get; init; }

    public string? Url { get; init; }

    public int Score { get; init; }

    /// <summary>Total comment count; absent for some items.</summary>
    public int? Descendants { get; init; }

    public bool Deleted { get; init; }

    public bool Dead { get; init; }
}
