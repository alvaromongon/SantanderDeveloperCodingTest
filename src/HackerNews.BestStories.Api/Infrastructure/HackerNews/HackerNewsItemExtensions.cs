using HackerNews.BestStories.Api.Models;

namespace HackerNews.BestStories.Api.Infrastructure.HackerNews;

internal static class HackerNewsItemExtensions
{
    private const string StoryType = "story";

    /// <summary>
    /// Maps a Hacker News item to the public story contract.
    /// </summary>
    /// <returns>The story, or <see langword="null"/> when the item is deleted, dead or not a story.</returns>
    public static StoryResponse? ToStoryResponse(this HackerNewsItem item)
    {
        if (item.Deleted || item.Dead || item.Type != StoryType)
        {
            return null;
        }

        return new StoryResponse(
            Title: item.Title ?? string.Empty,
            Uri: GetUri(item),
            PostedBy: item.By ?? string.Empty,
            Time: DateTimeOffset.FromUnixTimeSeconds(item.Time),
            Score: item.Score,
            CommentCount: item.Descendants ?? 0);
    }

    // Stories without a valid link (e.g. Ask HN) point to their Hacker News discussion page.
    private static Uri GetUri(HackerNewsItem item) =>
        Uri.TryCreate(item.Url, UriKind.Absolute, out var uri)
            ? uri
            : new Uri($"https://news.ycombinator.com/item?id={item.Id}");
}
