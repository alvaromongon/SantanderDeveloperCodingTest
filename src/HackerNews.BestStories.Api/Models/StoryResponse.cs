namespace HackerNews.BestStories.Api.Models;

/// <summary>
/// A Hacker News story as returned by the best stories endpoint.
/// </summary>
/// <param name="Title">Title of the story.</param>
/// <param name="Uri">Link of the story, or its Hacker News page when it has no link (e.g. Ask HN).</param>
/// <param name="PostedBy">Username of the author.</param>
/// <param name="Time">Creation time in UTC.</param>
/// <param name="Score">Score of the story.</param>
/// <param name="CommentCount">Total number of comments.</param>
public sealed record StoryResponse(
    string Title,
    Uri Uri,
    string PostedBy,
    DateTimeOffset Time,
    int Score,
    int CommentCount);
