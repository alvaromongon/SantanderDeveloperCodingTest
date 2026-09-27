using System.ComponentModel.DataAnnotations;

namespace HackerNews.BestStories.Api.Infrastructure.HackerNews;

/// <summary>
/// Settings of the Hacker News integration (configuration section <c>HackerNews</c>).
/// </summary>
internal sealed class HackerNewsOptions : IValidatableObject
{
    public const string SectionName = "HackerNews";

    /// <summary>Base address of the Hacker News API; must end with <c>/</c>.</summary>
    [Required]
    public Uri BaseAddress { get; set; } = new("https://hacker-news.firebaseio.com/v0/");

    /// <summary>How often the cached best stories are refreshed from Hacker News.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Lifetime of the cached data. Greater than <see cref="RefreshInterval"/> so the last good data
    /// keeps being served when a refresh fails.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>Maximum number of concurrent requests sent to Hacker News.</summary>
    [Range(1, 64)]
    public int MaxConcurrentRequests { get; set; } = 8;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!BaseAddress.IsAbsoluteUri)
        {
            yield return new ValidationResult(
                "The base address must be an absolute URI.", [nameof(BaseAddress)]);
        }

        if (CacheExpiration <= RefreshInterval)
        {
            yield return new ValidationResult(
                "The cache expiration must be greater than the refresh interval.",
                [nameof(CacheExpiration), nameof(RefreshInterval)]);
        }
    }
}
