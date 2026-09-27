using HackerNews.BestStories.Api.Infrastructure.HackerNews;

using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace HackerNews.BestStories.Api.Extensions;

internal static class HackerNewsExtensions
{
    // A ranking rebuild issues at most 1 + 200 calls and stampede protection allows only a few
    // rebuilds at once, so this queue never rejects legitimate calls.
    private const int RequestQueueLimit = 1_000;

    /// <summary>
    /// Registers the validated <see cref="HackerNewsOptions"/> and the resilient typed
    /// <see cref="IHackerNewsClient"/>.
    /// </summary>
    /// <remarks>
    /// The concurrency limiter of the resilience pipeline (its outermost strategy, shared by every
    /// client instance) bounds the calls in flight to <see cref="HackerNewsOptions.MaxConcurrentRequests"/>,
    /// retries included; the rest wait in a queue that does not count towards the timeouts.
    /// </remarks>
    public static IHostApplicationBuilder AddHackerNewsClient(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<HackerNewsOptions>()
            .Bind(builder.Configuration.GetSection(HackerNewsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddHttpClient<IHackerNewsClient, HackerNewsClient>((services, client) =>
                client.BaseAddress = services.GetRequiredService<IOptions<HackerNewsOptions>>().Value.BaseAddress)
            .AddStandardResilienceHandler()
            .Configure((resilience, services) =>
            {
                var limiter = resilience.RateLimiter.DefaultRateLimiterOptions;
                limiter.PermitLimit = services.GetRequiredService<IOptions<HackerNewsOptions>>().Value.MaxConcurrentRequests;
                limiter.QueueLimit = RequestQueueLimit;
            });

        return builder;
    }
}
