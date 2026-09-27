using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace HackerNews.BestStories.Api.ComponentTests.TestDoubles;

/// <summary>
/// Hosts the API in-process with Hacker News replaced by a <see cref="HackerNewsStub"/>, in the
/// given environment (<c>Development</c> by default).
/// </summary>
/// <remarks>
/// The periodic refresh is pushed far away so only the start warm-up and the requests reach the stub,
/// and the retry backoff is shortened so resilience tests stay fast.
/// </remarks>
internal sealed class BestStoriesApiFactory(HackerNewsStub hackerNews, string environment = "Development")
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("HackerNews:BaseAddress", hackerNews.BaseAddress);
        builder.UseSetting("HackerNews:RefreshInterval", "12:00:00");
        builder.UseSetting("HackerNews:CacheExpiration", "1.00:00:00");

        builder.ConfigureTestServices(services =>
            services.ConfigureAll<HttpStandardResilienceOptions>(options =>
                options.Retry.Delay = TimeSpan.FromMilliseconds(1)));
    }
}
