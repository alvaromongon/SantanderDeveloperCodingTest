using HackerNews.BestStories.Api.Infrastructure.HackerNews;
using HackerNews.BestStories.Api.Services;

using Microsoft.Extensions.Options;

namespace HackerNews.BestStories.Api.BackgroundServices;

/// <summary>
/// Keeps the cached best stories fresh: warms up the ranking on start and refreshes it from Hacker
/// News every <see cref="HackerNewsOptions.RefreshInterval"/>, independently of incoming traffic.
/// </summary>
/// <remarks>
/// A failed refresh is logged and the last good data keeps being served until it expires
/// (<see cref="HackerNewsOptions.CacheExpiration"/> is greater than the refresh interval).
/// Each run resolves the transient <see cref="IBestStoriesService"/> from its own scope.
/// </remarks>
internal sealed partial class BestStoriesCacheRefresher(
    IServiceScopeFactory scopeFactory,
    IOptions<HackerNewsOptions> options,
    TimeProvider timeProvider,
    ILogger<BestStoriesCacheRefresher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Created first so the refresh schedule does not drift with the warm-up duration.
        using var timer = new PeriodicTimer(options.Value.RefreshInterval, timeProvider);

        // Goes through GetOrCreateAsync, so it shares the single rebuild with cold-cache requests.
        // On stop, the cancellation propagates and the host treats it as a clean shutdown.
        await RunAsync(static (service, token) => service.GetBestStoriesAsync(1, token), stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunAsync(static (service, token) => service.RefreshAsync(token), stoppingToken);
        }
    }

    private async Task RunAsync(Func<IBestStoriesService, CancellationToken, Task> operation, CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IBestStoriesService>();

        try
        {
            await operation(service, stoppingToken);
        }
        catch (HackerNewsUnavailableException exception)
        {
            LogRefreshFailed(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "The best stories could not be refreshed from Hacker News; the last good data keeps being served.")]
    private static partial void LogRefreshFailed(ILogger logger, Exception exception);
}
