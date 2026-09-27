using System.Net;

using HackerNews.BestStories.Api.ComponentTests.TestDoubles;

namespace HackerNews.BestStories.Api.ComponentTests.Extensions;

public sealed class HackerNewsExtensionsTests : IAsyncDisposable
{
    private readonly HackerNewsStub _hackerNews = new();
    private readonly BestStoriesApiFactory _factory;

    public HackerNewsExtensionsTests() => _factory = new BestStoriesApiFactory(_hackerNews);

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        _hackerNews.Dispose();
    }

    [Fact]
    public async Task AddHackerNewsClient_TransientUpstreamFailure_RetriesAndSucceeds()
    {
        _hackerNews.GivenBestStoryIdsFailOnce(1);
        _hackerNews.GivenItem(1, HackerNewsStub.CreateStory(1, 100));
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/stories/best?count=1", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _hackerNews.CountCalls(HackerNewsStub.BestStoriesPath).Should().Be(2, "the 500 was retried once");
    }
}
