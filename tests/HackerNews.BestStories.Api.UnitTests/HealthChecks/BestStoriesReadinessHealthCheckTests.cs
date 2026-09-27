using HackerNews.BestStories.Api.HealthChecks;
using HackerNews.BestStories.Api.Services;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using NSubstitute;

namespace HackerNews.BestStories.Api.UnitTests.HealthChecks;

public sealed class BestStoriesReadinessHealthCheckTests
{
    private readonly IBestStoriesService _service = Substitute.For<IBestStoriesService>();

    private Task<HealthCheckResult> CheckHealthAsync() =>
        new BestStoriesReadinessHealthCheck(_service).CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task CheckHealthAsync_RankingCached_ReturnsHealthy()
    {
        _service.IsRankingCachedAsync(Arg.Any<CancellationToken>()).Returns(true);

        var result = await CheckHealthAsync();

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_RankingNotCached_ReturnsUnhealthy()
    {
        _service.IsRankingCachedAsync(Arg.Any<CancellationToken>()).Returns(false);

        var result = await CheckHealthAsync();

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().NotBeNullOrWhiteSpace();
    }
}
