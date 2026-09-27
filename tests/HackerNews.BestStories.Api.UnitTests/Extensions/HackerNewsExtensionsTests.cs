using HackerNews.BestStories.Api.Extensions;
using HackerNews.BestStories.Api.Infrastructure.HackerNews;
using HackerNews.BestStories.Api.UnitTests.TestDoubles;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HackerNews.BestStories.Api.UnitTests.Extensions;

public sealed class HackerNewsExtensionsTests
{
    private static ServiceProvider BuildServiceProvider(
        Dictionary<string, string?> configuration, HttpMessageHandler? primaryHandler = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(configuration);

        builder.AddHackerNewsClient();

        if (primaryHandler is not null)
        {
            builder.Services.AddHttpClient<IHackerNewsClient, HackerNewsClient>()
                .ConfigurePrimaryHttpMessageHandler(() => primaryHandler);
        }

        return builder.Services.BuildServiceProvider();
    }

    private static HackerNewsOptions GetOptions(Dictionary<string, string?> configuration)
    {
        using var provider = BuildServiceProvider(configuration);
        return provider.GetRequiredService<IOptions<HackerNewsOptions>>().Value;
    }

    [Fact]
    public void AddHackerNewsClient_NoConfiguration_UsesDefaults()
    {
        var options = GetOptions([]);

        options.Should().BeEquivalentTo(new HackerNewsOptions
        {
            BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/"),
            RefreshInterval = TimeSpan.FromMinutes(1),
            CacheExpiration = TimeSpan.FromMinutes(3),
            MaxConcurrentRequests = 8,
        });
    }

    [Fact]
    public void AddHackerNewsClient_Configuration_BindsHackerNewsSection()
    {
        var options = GetOptions(new()
        {
            ["HackerNews:BaseAddress"] = "http://hackernews-stub:8080/v0/",
            ["HackerNews:RefreshInterval"] = "00:00:30",
            ["HackerNews:CacheExpiration"] = "00:05:00",
            ["HackerNews:MaxConcurrentRequests"] = "4",
        });

        options.Should().BeEquivalentTo(new HackerNewsOptions
        {
            BaseAddress = new Uri("http://hackernews-stub:8080/v0/"),
            RefreshInterval = TimeSpan.FromSeconds(30),
            CacheExpiration = TimeSpan.FromMinutes(5),
            MaxConcurrentRequests = 4,
        });
    }

    [Theory]
    [InlineData("HackerNews:BaseAddress", "/v0/")]
    [InlineData("HackerNews:RefreshInterval", "00:00:00")]
    [InlineData("HackerNews:RefreshInterval", "-00:00:01")]
    [InlineData("HackerNews:CacheExpiration", "00:01:00")]
    [InlineData("HackerNews:CacheExpiration", "00:00:30")]
    [InlineData("HackerNews:MaxConcurrentRequests", "0")]
    [InlineData("HackerNews:MaxConcurrentRequests", "65")]
    public void AddHackerNewsClient_InvalidConfiguration_ThrowsOptionsValidationException(string key, string value)
    {
        var act = () => GetOptions(new() { [key] = value });

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public async Task AddHackerNewsClient_InvalidConfiguration_FailsOnHostStart()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection([new("HackerNews:MaxConcurrentRequests", "0")]);
        builder.AddHackerNewsClient();
        using var host = builder.Build();

        var act = () => host.StartAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OptionsValidationException>();
    }

    [Fact]
    public async Task AddHackerNewsClient_ResolvedClient_CallsConfiguredBaseAddress()
    {
        using var handler = new StubHttpMessageHandler().RespondWithJson("/v0/beststories.json", "[1]");
        using var provider = BuildServiceProvider(
            new() { ["HackerNews:BaseAddress"] = "http://hackernews-stub:8080/v0/" }, handler);

        var ids = await provider.GetRequiredService<IHackerNewsClient>()
            .GetBestStoryIdsAsync(TestContext.Current.CancellationToken);

        ids.Should().Equal(1);
        handler.Requests.Should().ContainSingle()
            .Which.Should().Be(new Uri("http://hackernews-stub:8080/v0/beststories.json"));
    }

    [Fact]
    public async Task AddHackerNewsClient_ConcurrentCallsFromSeveralClients_LimitsRequestsInFlight()
    {
        using var handler = new ConcurrencyTrackingHttpMessageHandler("null");
        using var provider = BuildServiceProvider(new() { ["HackerNews:MaxConcurrentRequests"] = "3" }, handler);

        // Each resolution is a new transient client: the limit must be shared by all of them.
        var calls = Enumerable.Range(1, 30).Select(id => provider.GetRequiredService<IHackerNewsClient>()
            .GetItemAsync(id, TestContext.Current.CancellationToken));
        await Task.WhenAll(calls);

        handler.MaxInFlight.Should().Be(3);
    }
}
