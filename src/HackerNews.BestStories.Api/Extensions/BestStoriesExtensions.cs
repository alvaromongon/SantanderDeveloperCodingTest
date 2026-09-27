using HackerNews.BestStories.Api.Services;

using Microsoft.Extensions.Caching.Hybrid;

namespace HackerNews.BestStories.Api.Extensions;

internal static class BestStoriesExtensions
{
    /// <summary>
    /// Registers <see cref="HybridCache"/> (in-memory) and the <see cref="IBestStoriesService"/>.
    /// Requires <see cref="HackerNewsExtensions.AddHackerNewsClient"/>.
    /// </summary>
    /// <remarks>
    /// The service is transient because it depends on the transient typed Hacker News client, whose
    /// handlers <c>IHttpClientFactory</c> rotates; singletons (e.g. the background refresher) must
    /// resolve it from a scope.
    /// </remarks>
    public static IHostApplicationBuilder AddBestStoriesService(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHybridCache();
        builder.Services.AddTransient<IBestStoriesService, BestStoriesService>();

        return builder;
    }
}
