namespace HackerNews.BestStories.Api.Services;

/// <summary>
/// The best stories could not be obtained from Hacker News (error status, malformed payload,
/// timeout or rejected call) and no cached ranking is available.
/// </summary>
internal sealed class HackerNewsUnavailableException : Exception
{
    public HackerNewsUnavailableException()
    {
    }

    public HackerNewsUnavailableException(string message)
        : base(message)
    {
    }

    public HackerNewsUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
