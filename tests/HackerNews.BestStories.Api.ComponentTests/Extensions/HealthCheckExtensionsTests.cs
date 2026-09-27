using System.Net;

using HackerNews.BestStories.Api.ComponentTests.TestDoubles;

namespace HackerNews.BestStories.Api.ComponentTests.Extensions;

public sealed class HealthCheckExtensionsTests : IAsyncDisposable
{
    private readonly HackerNewsStub _hackerNews = new();
    private readonly BestStoriesApiFactory _factory;

    public HealthCheckExtensionsTests() => _factory = new BestStoriesApiFactory(_hackerNews);

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        _hackerNews.Dispose();
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MapHealthCheckEndpoints_LiveWithHackerNewsUnavailable_ReturnsHealthy()
    {
        _hackerNews.GivenUnavailable();
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health/live", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(CancellationToken)).Should().Be("Healthy");
    }

    [Fact]
    public async Task MapHealthCheckEndpoints_ReadyWithColdCache_ReturnsServiceUnavailable()
    {
        _hackerNews.GivenUnavailable();
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health/ready", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync(CancellationToken)).Should().Be("Unhealthy");
    }

    [Fact]
    public async Task MapHealthCheckEndpoints_ReadyAfterWarmUp_ReturnsHealthy()
    {
        _hackerNews.GivenBestStories((1, 10));
        using var client = _factory.CreateClient();

        // The background refresher warms the cache up on start, without any request.
        HttpStatusCode status;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        do
        {
            using var response = await client.GetAsync("/health/ready", CancellationToken);
            status = response.StatusCode;
            if (status != HttpStatusCode.OK)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), CancellationToken);
            }
        }
        while (status != HttpStatusCode.OK && DateTimeOffset.UtcNow < deadline);

        status.Should().Be(HttpStatusCode.OK);
        _hackerNews.CountCalls(HackerNewsStub.BestStoriesPath).Should().Be(1, "readiness only reads the cache");
    }
}
