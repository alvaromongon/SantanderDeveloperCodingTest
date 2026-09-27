using HackerNews.BestStories.Api.Infrastructure.HackerNews;

using Microsoft.Extensions.Options;

namespace HackerNews.BestStories.Api.Extensions;

internal static class HackerNewsExtensions
{
    /// <summary>
    /// Registers the validated <see cref="HackerNewsOptions"/> and the resilient typed
    /// <see cref="IHackerNewsClient"/>.
    /// </summary>
    public static IHostApplicationBuilder AddHackerNewsClient(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<HackerNewsOptions>()
            .Bind(builder.Configuration.GetSection(HackerNewsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddHttpClient<IHackerNewsClient, HackerNewsClient>((services, client) =>
                client.BaseAddress = services.GetRequiredService<IOptions<HackerNewsOptions>>().Value.BaseAddress)
            .AddStandardResilienceHandler();

        return builder;
    }
}
