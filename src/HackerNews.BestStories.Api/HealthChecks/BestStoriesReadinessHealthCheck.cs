using HackerNews.BestStories.Api.Services;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HackerNews.BestStories.Api.HealthChecks;

/// <summary>
/// Ready when the best stories ranking is cached, so requests are served without waiting for a
/// rebuild from Hacker News. Only reads the cache; it never calls Hacker News.
/// </summary>
internal sealed class BestStoriesReadinessHealthCheck(IBestStoriesService service) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await service.IsRankingCachedAsync(cancellationToken)
            ? HealthCheckResult.Healthy("The best stories ranking is cached.")
            : HealthCheckResult.Unhealthy("The best stories ranking is not cached yet.");
}
