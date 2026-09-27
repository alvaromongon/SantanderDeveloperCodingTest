using System.Net;
using System.Text;

namespace HackerNews.BestStories.Api.UnitTests.TestDoubles;

/// <summary>
/// Fake <see cref="HttpMessageHandler"/> that holds every request for a short delay and records the
/// maximum number of requests in flight at the same time.
/// </summary>
internal sealed class ConcurrencyTrackingHttpMessageHandler(string json) : HttpMessageHandler
{
    private static readonly TimeSpan ResponseDelay = TimeSpan.FromMilliseconds(20);

    private int _inFlight;
    private int _maxInFlight;

    public int MaxInFlight => Volatile.Read(ref _maxInFlight);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var inFlight = Interlocked.Increment(ref _inFlight);
        InterlockedMax(ref _maxInFlight, inFlight);

        try
        {
            await Task.Delay(ResponseDelay, cancellationToken);

            // Ownership of the response passes to the caller (HttpClient), which disposes it.
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    private static void InterlockedMax(ref int location, int value)
    {
        var current = Volatile.Read(ref location);
        while (value > current)
        {
            var previous = Interlocked.CompareExchange(ref location, value, current);
            if (previous == current)
            {
                return;
            }

            current = previous;
        }
    }
}
