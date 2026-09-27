using System.ComponentModel.DataAnnotations;

namespace HackerNews.BestStories.Api.RateLimiting;

/// <summary>
/// Settings of the global limiter of concurrent incoming requests (configuration section
/// <c>RequestConcurrency</c>). Requests above the limit are rejected with <c>429 Too Many Requests</c>.
/// </summary>
internal sealed class RequestConcurrencyOptions
{
    public const string SectionName = "RequestConcurrency";

    /// <summary>Maximum number of requests processed at the same time.</summary>
    [Range(1, 100_000)]
    public int PermitLimit { get; set; } = 1_000;

    /// <summary>Requests that wait for a permit before being rejected; 0 rejects them immediately.</summary>
    [Range(0, 100_000)]
    public int QueueLimit { get; set; }
}
