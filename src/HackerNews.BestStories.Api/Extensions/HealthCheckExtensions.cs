using HackerNews.BestStories.Api.HealthChecks;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace HackerNews.BestStories.Api.Extensions;

internal static class HealthCheckExtensions
{
    private const string ReadyTag = "ready";

    /// <summary>
    /// Registers the health checks: <see cref="BestStoriesReadinessHealthCheck"/> is tagged as a
    /// readiness check. Requires <see cref="BestStoriesExtensions.AddBestStoriesService"/>.
    /// </summary>
    public static IHostApplicationBuilder AddBestStoriesHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddCheck<BestStoriesReadinessHealthCheck>("best-stories-cache", tags: [ReadyTag]);

        return builder;
    }

    /// <summary>
    /// Maps <c>/health/live</c> (the process responds; runs no check) and <c>/health/ready</c>
    /// (the readiness checks pass: the best stories are cached). Both answer <c>200</c> when healthy
    /// and <c>503</c> otherwise, and are excluded from the request concurrency limiter so they keep
    /// answering under load.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthCheckEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .DisableRateLimiting();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) })
            .DisableRateLimiting();

        return app;
    }
}
