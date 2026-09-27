using HackerNews.BestStories.Api.BackgroundServices;
using HackerNews.BestStories.Api.Infrastructure.HackerNews;
using HackerNews.BestStories.Api.Models;
using HackerNews.BestStories.Api.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace HackerNews.BestStories.Api.UnitTests.BackgroundServices;

public sealed class BestStoriesCacheRefresherTests : IAsyncDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(5);

    private readonly IBestStoriesService _service = Substitute.For<IBestStoriesService>();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly FakeLogger<BestStoriesCacheRefresher> _logger = new();
    private readonly SemaphoreSlim _warmedUp = new(0);
    private readonly SemaphoreSlim _refreshed = new(0);
    private readonly ServiceProvider _provider;
    private readonly BestStoriesCacheRefresher _refresher;
    private int _resolvedServices;

    public BestStoriesCacheRefresherTests()
    {
        _service.GetBestStoriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        _service.When(service => service.GetBestStoriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()))
            .Do(_ => _warmedUp.Release());
        _service.RefreshAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _service.When(service => service.RefreshAsync(Arg.Any<CancellationToken>())).Do(_ => _refreshed.Release());

        // Transient like the real service; counts the instances resolved from the scopes.
        _provider = new ServiceCollection()
            .AddTransient(_ =>
            {
                Interlocked.Increment(ref _resolvedServices);
                return _service;
            })
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        _refresher = new BestStoriesCacheRefresher(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new HackerNewsOptions { RefreshInterval = RefreshInterval }),
            _timeProvider,
            _logger);
    }

    public async ValueTask DisposeAsync()
    {
        await _refresher.StopAsync(CancellationToken.None);
        _refresher.Dispose();
        await _provider.DisposeAsync();
        _warmedUp.Dispose();
        _refreshed.Dispose();
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    // ExecuteAsync runs on the thread pool: the refresh timer exists once the warm-up has started.
    private async Task StartAsync()
    {
        await _refresher.StartAsync(CancellationToken);

        (await _warmedUp.WaitAsync(SignalTimeout, CancellationToken)).Should().BeTrue("a warm-up was expected");
    }

    private async Task AdvanceToNextRefreshAsync()
    {
        _timeProvider.Advance(RefreshInterval);

        (await _refreshed.WaitAsync(SignalTimeout, CancellationToken)).Should().BeTrue("a refresh was expected");
    }

    [Fact]
    public async Task StartAsync_ColdCache_WarmsUpRankingOnce()
    {
        await StartAsync();

        await _service.Received(1).GetBestStoriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _service.DidNotReceive().RefreshAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_BeforeRefreshInterval_DoesNotRefresh()
    {
        await StartAsync();

        _timeProvider.Advance(RefreshInterval - TimeSpan.FromTicks(1));

        await _service.DidNotReceive().RefreshAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_EachRefreshInterval_RefreshesCache()
    {
        await StartAsync();

        await AdvanceToNextRefreshAsync();
        await AdvanceToNextRefreshAsync();
        await AdvanceToNextRefreshAsync();

        await _service.Received(3).RefreshAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_EachRefresh_ResolvesServiceFromNewScope()
    {
        await StartAsync();

        await AdvanceToNextRefreshAsync();
        await AdvanceToNextRefreshAsync();

        _resolvedServices.Should().Be(3, "the warm-up and each refresh resolve the transient service");
    }

    [Fact]
    public async Task StartAsync_RefreshFails_LogsWarningAndKeepsRefreshing()
    {
        _service.RefreshAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new HackerNewsUnavailableException("down")), Task.CompletedTask);
        await StartAsync();

        await AdvanceToNextRefreshAsync();
        await AdvanceToNextRefreshAsync();

        await _service.Received(2).RefreshAsync(Arg.Any<CancellationToken>());
        _refresher.ExecuteTask!.IsFaulted.Should().BeFalse("a failed refresh must not stop the refresher");
        var failure = _logger.Collector.GetSnapshot().Should().ContainSingle(record => record.Level == LogLevel.Warning).Subject;
        failure.Exception.Should().BeOfType<HackerNewsUnavailableException>();
    }

    [Fact]
    public async Task StartAsync_WarmUpFails_LogsWarningAndKeepsRefreshing()
    {
        _service.GetBestStoriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HackerNewsUnavailableException("down"));
        await StartAsync();

        await AdvanceToNextRefreshAsync();

        await _service.Received(1).RefreshAsync(Arg.Any<CancellationToken>());
        var failure = _logger.Collector.GetSnapshot().Should().ContainSingle(record => record.Level == LogLevel.Warning).Subject;
        failure.Exception.Should().BeOfType<HackerNewsUnavailableException>();
    }

    [Fact]
    public async Task StopAsync_Started_StopsRefreshing()
    {
        await StartAsync();

        await _refresher.StopAsync(CancellationToken);
        _timeProvider.Advance(RefreshInterval);

        _refresher.ExecuteTask!.IsFaulted.Should().BeFalse();
        await _service.DidNotReceive().RefreshAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopAsync_RefreshInProgress_CancelsRefreshWithoutFailure()
    {
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.RefreshAsync(Arg.Any<CancellationToken>()).Returns(async call =>
        {
            refreshStarted.SetResult();
            await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
        });
        await StartAsync();
        _timeProvider.Advance(RefreshInterval);
        await refreshStarted.Task.WaitAsync(SignalTimeout, CancellationToken);

        await _refresher.StopAsync(CancellationToken);

        _refresher.ExecuteTask!.IsFaulted.Should().BeFalse();
        _logger.Collector.Count.Should().Be(0, "a cancelled refresh is not a failure");
    }

    [Fact]
    public async Task StopAsync_WarmUpInProgress_CancelsWarmUpWithoutFailure()
    {
        _service.GetBestStoriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
            return [];
        });
        await StartAsync();

        await _refresher.StopAsync(CancellationToken);

        _refresher.ExecuteTask!.IsFaulted.Should().BeFalse();
        _logger.Collector.Count.Should().Be(0, "a cancelled warm-up is not a failure");
    }
}
