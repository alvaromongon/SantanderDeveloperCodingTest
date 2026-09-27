using HackerNews.BestStories.Api.Extensions;
using HackerNews.BestStories.Api.RateLimiting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HackerNews.BestStories.Api.UnitTests.Extensions;

public sealed class RateLimitingExtensionsTests
{
    private static HostApplicationBuilder CreateBuilder(Dictionary<string, string?> configuration)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(configuration);

        builder.AddRequestConcurrencyLimiter();

        return builder;
    }

    private static RequestConcurrencyOptions GetOptions(Dictionary<string, string?> configuration)
    {
        using var provider = CreateBuilder(configuration).Services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<RequestConcurrencyOptions>>().Value;
    }

    [Fact]
    public void AddRequestConcurrencyLimiter_NoConfiguration_UsesDefaults()
    {
        var options = GetOptions([]);

        options.Should().BeEquivalentTo(new RequestConcurrencyOptions { PermitLimit = 1_000, QueueLimit = 0 });
    }

    [Fact]
    public void AddRequestConcurrencyLimiter_Configuration_BindsRequestConcurrencySection()
    {
        var options = GetOptions(new()
        {
            ["RequestConcurrency:PermitLimit"] = "50",
            ["RequestConcurrency:QueueLimit"] = "10",
        });

        options.Should().BeEquivalentTo(new RequestConcurrencyOptions { PermitLimit = 50, QueueLimit = 10 });
    }

    [Theory]
    [InlineData("RequestConcurrency:PermitLimit", "0")]
    [InlineData("RequestConcurrency:PermitLimit", "100001")]
    [InlineData("RequestConcurrency:QueueLimit", "-1")]
    [InlineData("RequestConcurrency:QueueLimit", "100001")]
    public void AddRequestConcurrencyLimiter_InvalidConfiguration_ThrowsOptionsValidationException(string key, string value)
    {
        var act = () => GetOptions(new() { [key] = value });

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public async Task AddRequestConcurrencyLimiter_InvalidConfiguration_FailsOnHostStart()
    {
        using var host = CreateBuilder(new() { ["RequestConcurrency:PermitLimit"] = "0" }).Build();

        var act = () => host.StartAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OptionsValidationException>();
    }
}
