using System.Net;

using HackerNews.BestStories.Api.ComponentTests.TestDoubles;

namespace HackerNews.BestStories.Api.ComponentTests;

public sealed class ProgramTests : IAsyncDisposable
{
    private readonly HackerNewsStub _hackerNews = new();
    private readonly BestStoriesApiFactory _factory;

    public ProgramTests() => _factory = new BestStoriesApiFactory(_hackerNews);

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        _hackerNews.Dispose();
    }

    [Fact]
    public async Task GetOpenApiDocument_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
