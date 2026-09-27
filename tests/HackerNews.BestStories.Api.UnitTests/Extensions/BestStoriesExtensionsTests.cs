using HackerNews.BestStories.Api.BackgroundServices;
using HackerNews.BestStories.Api.Extensions;
using HackerNews.BestStories.Api.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HackerNews.BestStories.Api.UnitTests.Extensions;

public sealed class BestStoriesExtensionsTests
{
    private static ServiceProvider BuildServiceProvider()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.AddHackerNewsClient();

        builder.AddBestStoriesService();

        return builder.Services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void AddBestStoriesService_ResolvedService_IsTransient()
    {
        using var provider = BuildServiceProvider();

        var first = provider.GetRequiredService<IBestStoriesService>();
        var second = provider.GetRequiredService<IBestStoriesService>();

        first.Should().BeOfType<BestStoriesService>();
        second.Should().NotBeSameAs(first);
    }

    [Fact]
    public void AddBestStoriesService_HostedServices_IncludeCacheRefresher()
    {
        using var provider = BuildServiceProvider();

        provider.GetServices<IHostedService>().Should().ContainSingle(service => service is BestStoriesCacheRefresher);
    }
}
