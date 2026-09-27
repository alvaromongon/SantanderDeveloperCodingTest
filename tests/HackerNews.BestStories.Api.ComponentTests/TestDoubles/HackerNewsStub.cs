using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace HackerNews.BestStories.Api.ComponentTests.TestDoubles;

/// <summary>
/// In-process stub of the Hacker News API (WireMock.Net) that records the calls it receives.
/// </summary>
internal sealed class HackerNewsStub : IDisposable
{
    public const string BestStoriesPath = "/v0/beststories.json";

    private readonly WireMockServer _server = WireMockServer.Start();

    /// <summary>Base address to configure as <c>HackerNews:BaseAddress</c>.</summary>
    public string BaseAddress => $"{_server.Url}/v0/";

    public void Dispose() => _server.Stop();

    public static string ItemPath(long id) => $"/v0/item/{id}.json";

    /// <summary>Stubs the best story IDs and one story per ID, in the given Hacker News order.</summary>
    public void GivenBestStories(params (long Id, int Score)[] stories) =>
        GivenBestStories(TimeSpan.Zero, stories);

    /// <summary>Same as <see cref="GivenBestStories((long Id, int Score)[])"/> with a latency per call.</summary>
    public void GivenBestStories(TimeSpan delay, params (long Id, int Score)[] stories)
    {
        GivenJson(BestStoriesPath, stories.Select(story => story.Id).ToArray(), delay);

        foreach (var (id, score) in stories)
        {
            GivenItem(id, CreateStory(id, score), delay);
        }
    }

    public void GivenItem(long id, object item, TimeSpan delay = default) => GivenJson(ItemPath(id), item, delay);

    /// <summary>Every Hacker News call fails with <c>500 Internal Server Error</c>.</summary>
    public void GivenUnavailable()
    {
        _server.Reset();
        _server.Given(Request.Create().UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));
    }

    /// <summary>The first best story IDs call fails with <c>500</c>; the following ones return <paramref name="ids"/>.</summary>
    public void GivenBestStoryIdsFailOnce(params long[] ids)
    {
        const string scenario = "best stories fail once";

        _server.Given(Request.Create().WithPath(BestStoriesPath).UsingGet())
            .InScenario(scenario)
            .WillSetStateTo("recovered")
            .RespondWith(Response.Create().WithStatusCode(500));

        _server.Given(Request.Create().WithPath(BestStoriesPath).UsingGet())
            .InScenario(scenario)
            .WhenStateIs("recovered")
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(ids));
    }

    public int CountCalls(string path) =>
        _server.LogEntries.Count(entry => entry.RequestMessage?.Path == path);

    public int CountCalls() => _server.LogEntries.Count;

    public static object CreateStory(long id, int score) => new
    {
        id,
        type = "story",
        by = $"author{id}",
        time = 1_570_887_781,
        title = $"Story {id}",
        url = $"https://example.com/{id}",
        score,
        descendants = 1,
    };

    private void GivenJson(string path, object body, TimeSpan delay)
    {
        var response = Response.Create().WithStatusCode(200).WithBodyAsJson(body);

        _server.Given(Request.Create().WithPath(path).UsingGet())
            .RespondWith(delay > TimeSpan.Zero ? response.WithDelay(delay) : response);
    }
}
