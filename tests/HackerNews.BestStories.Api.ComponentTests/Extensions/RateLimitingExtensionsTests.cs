using System.Net;
using System.Net.Http.Json;

using HackerNews.BestStories.Api.ComponentTests.TestDoubles;
using HackerNews.BestStories.Api.Models;
using HackerNews.BestStories.Api.Services;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace HackerNews.BestStories.Api.ComponentTests.Extensions;

public sealed class RateLimitingExtensionsTests : IAsyncDisposable
{
    private const int BlockingCount = 5;

    private readonly HackerNewsStub _hackerNews = new();
    private readonly BestStoriesApiFactory _factory;
    private readonly WebApplicationFactory<Program> _limitedFactory;
    private readonly TaskCompletionSource _requestEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<IReadOnlyList<StoryResponse>> _releaseRequest =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public RateLimitingExtensionsTests()
    {
        // A request for BlockingCount stories holds the only permit until the test releases it.
        var service = Substitute.For<IBestStoriesService>();
        service.GetBestStoriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        service.GetBestStoriesAsync(BlockingCount, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            _requestEntered.TrySetResult();
            return _releaseRequest.Task;
        });
        service.IsRankingCachedAsync(Arg.Any<CancellationToken>()).Returns(true);

        _factory = new BestStoriesApiFactory(_hackerNews);
        _limitedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RequestConcurrency:PermitLimit", "1");
            builder.UseSetting("RequestConcurrency:QueueLimit", "0");
            builder.ConfigureTestServices(services => services.AddTransient(_ => service));
        });
    }

    public async ValueTask DisposeAsync()
    {
        _releaseRequest.TrySetResult([]);
        await _factory.DisposeAsync();
        _hackerNews.Dispose();
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private async Task<Task<HttpResponseMessage>> HoldOnlyPermitAsync(HttpClient client)
    {
        var request = client.GetAsync($"/api/stories/best?count={BlockingCount}", CancellationToken);
        await _requestEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
        return request;
    }

    [Fact]
    public async Task AddRequestConcurrencyLimiter_LimitReached_ReturnsTooManyRequestsProblem()
    {
        using var client = _limitedFactory.CreateClient();
        var blockedRequest = await HoldOnlyPermitAsync(client);

        using var rejected = await client.GetAsync("/api/stories/best?count=1", CancellationToken);

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejected.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await rejected.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken);
        problem!.Status.Should().Be(StatusCodes.Status429TooManyRequests);

        _releaseRequest.SetResult([]);
        using var completed = await blockedRequest;
        completed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AddRequestConcurrencyLimiter_PermitReleased_AcceptsNextRequest()
    {
        using var client = _limitedFactory.CreateClient();
        var blockedRequest = await HoldOnlyPermitAsync(client);
        _releaseRequest.SetResult([]);
        using var completed = await blockedRequest;

        using var response = await client.GetAsync("/api/stories/best?count=1", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task AddRequestConcurrencyLimiter_LimitReached_DoesNotLimitHealthChecks(string path)
    {
        using var client = _limitedFactory.CreateClient();
        await HoldOnlyPermitAsync(client);

        using var response = await client.GetAsync(path, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
