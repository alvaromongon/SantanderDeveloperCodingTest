using System.Threading.RateLimiting;

using HackerNews.BestStories.Api.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace HackerNews.BestStories.Api.Extensions;

internal static class RateLimitingExtensions
{
    private const string GlobalPartition = "global";

    /// <summary>
    /// Registers the validated <see cref="RequestConcurrencyOptions"/> and a global concurrency limiter
    /// shared by every request, which rejects the excess with <c>429 Too Many Requests</c>.
    /// </summary>
    /// <remarks>
    /// Requires <c>UseRateLimiter()</c>. Endpoints that must keep answering under load (health checks)
    /// opt out with <c>DisableRateLimiting()</c>.
    /// </remarks>
    public static IHostApplicationBuilder AddRequestConcurrencyLimiter(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<RequestConcurrencyOptions>()
            .Bind(builder.Configuration.GetSection(RequestConcurrencyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddRateLimiter(options => options.RejectionStatusCode = StatusCodes.Status429TooManyRequests);
        builder.Services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RequestConcurrencyOptions>>((options, concurrency) =>
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                    RateLimitPartition.GetConcurrencyLimiter(GlobalPartition, _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = concurrency.Value.PermitLimit,
                        QueueLimit = concurrency.Value.QueueLimit,
                    })));

        return builder;
    }
}
