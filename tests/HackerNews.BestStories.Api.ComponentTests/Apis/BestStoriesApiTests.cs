using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using HackerNews.BestStories.Api.ComponentTests.TestDoubles;
using HackerNews.BestStories.Api.Models;
using HackerNews.BestStories.Api.Services;

using Microsoft.Extensions.DependencyInjection;

namespace HackerNews.BestStories.Api.ComponentTests.Apis;

public sealed class BestStoriesApiTests : IAsyncDisposable
{
    private const string ProblemJson = "application/problem+json";

    private readonly HackerNewsStub _hackerNews = new();
    private readonly BestStoriesApiFactory _factory;

    public BestStoriesApiTests() => _factory = new BestStoriesApiFactory(_hackerNews);

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        _hackerNews.Dispose();
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private static async Task<IReadOnlyList<StoryResponse>> GetBestStoriesAsync(HttpClient client, int count)
    {
        using var response = await client.GetAsync($"/api/stories/best?count={count}", CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<StoryResponse[]>(CancellationToken))!;
    }

    private static IEnumerable<string> Titles(IEnumerable<StoryResponse> stories) =>
        stories.Select(story => story.Title);

    [Fact]
    public async Task GetBestStories_ValidCount_ReturnsStoryJsonContract()
    {
        _hackerNews.GivenBestStories((21168364, 1716));
        _hackerNews.GivenItem(21168364, new
        {
            id = 21168364,
            type = "story",
            by = "ismaildonmez",
            time = 1_570_887_781,
            title = "A uBlock Origin update was rejected from the Chrome Web Store",
            url = "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
            score = 1716,
            descendants = 572,
            kids = new[] { 21168530 },
        });
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/stories/best?count=1", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        var expected = JsonNode.Parse("""
            [
              {
                "title": "A uBlock Origin update was rejected from the Chrome Web Store",
                "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
                "postedBy": "ismaildonmez",
                "time": "2019-10-12T13:43:01+00:00",
                "score": 1716,
                "commentCount": 572
              }
            ]
            """);
        JsonNode.DeepEquals(json, expected).Should().BeTrue($"the response was {json}");
    }

    [Fact]
    public async Task GetBestStories_UnorderedScores_ReturnsStoriesOrderedByScoreDescending()
    {
        _hackerNews.GivenBestStories((1, 50), (2, 300), (3, 120), (4, 120));
        using var client = _factory.CreateClient();

        var stories = await GetBestStoriesAsync(client, 4);

        Titles(stories).Should().Equal("Story 2", "Story 3", "Story 4", "Story 1");
    }

    [Fact]
    public async Task GetBestStories_CountLowerThanAvailable_ReturnsBestCountStories()
    {
        _hackerNews.GivenBestStories((1, 50), (2, 300), (3, 120));
        using var client = _factory.CreateClient();

        var stories = await GetBestStoriesAsync(client, 2);

        Titles(stories).Should().Equal("Story 2", "Story 3");
    }

    [Theory]
    [InlineData(500)]
    [InlineData(int.MaxValue)]
    public async Task GetBestStories_CountGreaterThanAvailable_ReturnsAllStories(int count)
    {
        _hackerNews.GivenBestStories((1, 50), (2, 300), (3, 120));
        using var client = _factory.CreateClient();

        var stories = await GetBestStoriesAsync(client, count);

        stories.Should().HaveCount(3);
    }

    public static TheoryData<string, string, string> InvalidCounts()
    {
        const string required = "The count query parameter is required.";
        const string invalid = "count must be an integer greater than or equal to 1.";
        (string Query, string Error)[] cases =
        [
            ("", required),
            ("?count=", required),
            ("?count=abc", invalid),
            ("?count=1.5", invalid),
            ("?count=99999999999", invalid),
            ("?count=0", invalid),
            ("?count=-1", invalid),
        ];

        var data = new TheoryData<string, string, string>();
        foreach (var environment in (string[])["Development", "Production"])
        {
            foreach (var (query, error) in cases)
            {
                data.Add(environment, query, error);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(InvalidCounts))]
    public async Task GetBestStories_InvalidCount_ReturnsValidationProblemWithCountError(
        string environment, string query, string expectedError)
    {
        await using var factory = new BestStoriesApiFactory(_hackerNews, environment);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/stories/best{query}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync(CancellationToken))!;
        problem["errors"]!["count"]!.AsArray().Select(error => (string?)error)
            .Should().Equal([expectedError], $"the problem was {problem}");
    }

    [Fact]
    public async Task GetBestStories_HackerNewsDownAndColdCache_ReturnsServiceUnavailableProblem()
    {
        _hackerNews.GivenUnavailable();
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/stories/best?count=10", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
    }

    [Fact]
    public async Task GetBestStories_HackerNewsDownAndWarmCache_ReturnsLastGoodStories()
    {
        _hackerNews.GivenBestStories((1, 50), (2, 300));
        using var client = _factory.CreateClient();
        var before = await GetBestStoriesAsync(client, 10);
        _hackerNews.GivenUnavailable();

        // What the background refresher does on each interval.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var refresh = () => scope.ServiceProvider.GetRequiredService<IBestStoriesService>().RefreshAsync(CancellationToken);
            await refresh.Should().ThrowAsync<HackerNewsUnavailableException>();
        }

        var after = await GetBestStoriesAsync(client, 10);

        Titles(after).Should().Equal(Titles(before));
    }

    [Fact]
    public async Task GetBestStories_ConcurrentRequestsOnColdCache_CallHackerNewsOncePerResource()
    {
        var stories = Enumerable.Range(1, 200).Select(id => ((long)id, id)).ToArray();
        _hackerNews.GivenBestStories(TimeSpan.FromMilliseconds(20), stories);
        using var client = _factory.CreateClient();

        var responses = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => GetBestStoriesAsync(client, 200)));

        responses.Should().AllSatisfy(response => response.Should().HaveCount(200));
        _hackerNews.CountCalls(HackerNewsStub.BestStoriesPath).Should().Be(1);
        _hackerNews.CountCalls().Should().Be(1 + 200);
    }

    [Fact]
    public async Task GetOpenApiDocument_BestStoriesOperation_DocumentsCountAndResponses()
    {
        using var client = _factory.CreateClient();

        var document = await client.GetFromJsonAsync<JsonNode>("/openapi/v1.json", CancellationToken);

        var operation = document!["paths"]!["/api/stories/best"]!["get"]!;
        var count = operation["parameters"]!.AsArray().Single(parameter => (string?)parameter!["name"] == "count")!;
        ((bool?)count["required"]).Should().BeTrue();
        ((int?)count["schema"]!["minimum"]).Should().Be(1);
        count["schema"]!["maximum"].Should().BeNull("there is no upper limit, only the stories Hacker News provides");
        operation["responses"]!.AsObject().Select(response => response.Key)
            .Should().BeEquivalentTo(["200", "400", "503"]);
    }
}
