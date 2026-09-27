using System.Text.Json;

using HackerNews.BestStories.Api.Infrastructure.HackerNews;
using HackerNews.BestStories.Api.Models;
using HackerNews.BestStories.Api.Services;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Polly.Timeout;

namespace HackerNews.BestStories.Api.UnitTests.Services;

public sealed class BestStoriesServiceTests : IDisposable
{
    private readonly IHackerNewsClient _client = Substitute.For<IHackerNewsClient>();
    private readonly ServiceProvider _cacheProvider;
    private readonly HybridCache _cache;

    public BestStoriesServiceTests()
    {
        _cacheProvider = new ServiceCollection().AddHybridCache().Services.BuildServiceProvider();
        _cache = _cacheProvider.GetRequiredService<HybridCache>();
    }

    public void Dispose() => _cacheProvider.Dispose();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    // Each service instance shares the cache, like transient instances resolved per request.
    private BestStoriesService CreateService() =>
        new(_client, _cache, Options.Create(new HackerNewsOptions()), NullLogger<BestStoriesService>.Instance);

    private void GivenBestStories(params (long Id, int Score)[] stories)
    {
        _client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>()).Returns(stories.Select(story => story.Id).ToArray());

        foreach (var (id, score) in stories)
        {
            GivenItem(id, score);
        }
    }

    private void GivenItem(long id, int score) =>
        _client.GetItemAsync(id, Arg.Any<CancellationToken>()).Returns(CreateStory(id, score));

    private static HackerNewsItem CreateStory(long id, int score) => new()
    {
        Id = id,
        Type = "story",
        By = $"author{id}",
        Time = 1_570_887_781,
        Title = $"Story {id}",
        Url = $"https://example.com/{id}",
        Score = score,
        Descendants = 1,
    };

    private static IEnumerable<string> Titles(IEnumerable<StoryResponse> stories) =>
        stories.Select(story => story.Title);

    [Fact]
    public async Task GetBestStoriesAsync_UnorderedScores_ReturnsStoriesOrderedByScoreDescending()
    {
        GivenBestStories((1, 50), (2, 300), (3, 120));

        var stories = await CreateService().GetBestStoriesAsync(3, CancellationToken);

        Titles(stories).Should().Equal("Story 2", "Story 3", "Story 1");
    }

    [Fact]
    public async Task GetBestStoriesAsync_ValidItem_ReturnsMappedStory()
    {
        GivenBestStories((8863, 111));

        var stories = await CreateService().GetBestStoriesAsync(1, CancellationToken);

        stories.Should().ContainSingle().Which.Should().Be(new StoryResponse(
            Title: "Story 8863",
            Uri: new Uri("https://example.com/8863"),
            PostedBy: "author8863",
            Time: DateTimeOffset.FromUnixTimeSeconds(1_570_887_781),
            Score: 111,
            CommentCount: 1));
    }

    [Fact]
    public async Task GetBestStoriesAsync_TiedScores_KeepsHackerNewsOrder()
    {
        GivenBestStories((4, 100), (2, 200), (3, 100), (1, 100));

        var stories = await CreateService().GetBestStoriesAsync(4, CancellationToken);

        Titles(stories).Should().Equal("Story 2", "Story 4", "Story 3", "Story 1");
    }

    [Fact]
    public async Task GetBestStoriesAsync_CountLessThanAvailable_ReturnsTopCount()
    {
        GivenBestStories((1, 10), (2, 30), (3, 20));

        var stories = await CreateService().GetBestStoriesAsync(2, CancellationToken);

        Titles(stories).Should().Equal("Story 2", "Story 3");
    }

    [Fact]
    public async Task GetBestStoriesAsync_CountGreaterThanAvailable_ReturnsAllStories()
    {
        GivenBestStories((1, 10), (2, 30));

        var stories = await CreateService().GetBestStoriesAsync(500, CancellationToken);

        Titles(stories).Should().Equal("Story 2", "Story 1");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetBestStoriesAsync_CountNotPositive_ThrowsArgumentOutOfRangeException(int count)
    {
        var act = () => CreateService().GetBestStoriesAsync(count, CancellationToken);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        await _client.DidNotReceive().GetBestStoryIdsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBestStoriesAsync_MissingOrExcludedItems_ReturnsOnlyStories()
    {
        GivenBestStories((1, 10), (2, 20), (3, 30), (4, 40));
        _client.GetItemAsync(2, Arg.Any<CancellationToken>()).Returns((HackerNewsItem?)null);
        _client.GetItemAsync(3, Arg.Any<CancellationToken>()).Returns(CreateStory(3, 30) with { Dead = true });
        _client.GetItemAsync(4, Arg.Any<CancellationToken>()).Returns(CreateStory(4, 40) with { Type = "job" });

        var stories = await CreateService().GetBestStoriesAsync(10, CancellationToken);

        Titles(stories).Should().Equal("Story 1");
    }

    [Fact]
    public async Task GetBestStoriesAsync_CachedRanking_DoesNotCallClientAgain()
    {
        GivenBestStories((1, 10), (2, 20));
        await CreateService().GetBestStoriesAsync(2, CancellationToken);
        _client.ClearReceivedCalls();

        var stories = await CreateService().GetBestStoriesAsync(1, CancellationToken);

        Titles(stories).Should().Equal("Story 2");
        _client.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task GetBestStoriesAsync_ManyConcurrentCallsOnColdCache_RebuildsRankingOnce()
    {
        GivenBestStories((1, 10), (2, 20));
        var idsRequested = new TaskCompletionSource<IReadOnlyList<long>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>()).Returns(idsRequested.Task);

        var calls = Enumerable.Range(0, 100)
            .Select(_ => CreateService().GetBestStoriesAsync(2, CancellationToken))
            .ToArray();
        idsRequested.SetResult([1, 2]);
        var results = await Task.WhenAll(calls);

        results.Should().AllSatisfy(stories => Titles(stories).Should().Equal("Story 2", "Story 1"));
        await _client.Received(1).GetBestStoryIdsAsync(Arg.Any<CancellationToken>());
        await _client.Received(1).GetItemAsync(1, Arg.Any<CancellationToken>());
        await _client.Received(1).GetItemAsync(2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBestStoriesAsync_SomeItemsFail_SkipsFailedItems()
    {
        GivenBestStories((1, 10), (2, 20), (3, 30));
        _client.GetItemAsync(2, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("Boom"));
        _client.GetItemAsync(3, Arg.Any<CancellationToken>()).ThrowsAsync(new JsonException("Malformed"));

        var stories = await CreateService().GetBestStoriesAsync(3, CancellationToken);

        Titles(stories).Should().Equal("Story 1");
    }

    [Fact]
    public async Task GetBestStoriesAsync_AllItemsFail_ThrowsHackerNewsUnavailableException()
    {
        GivenBestStories((1, 10), (2, 20));
        _client.GetItemAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Boom"));

        var act = () => CreateService().GetBestStoriesAsync(2, CancellationToken);

        await act.Should().ThrowAsync<HackerNewsUnavailableException>();
    }

    public static TheoryData<Exception> UpstreamFailures => new()
    {
        new HttpRequestException("Boom"),
        new JsonException("Malformed"),
        new TimeoutRejectedException("Timed out"),
    };

    [Theory]
    [MemberData(nameof(UpstreamFailures))]
    public async Task GetBestStoriesAsync_BestStoryIdsFail_ThrowsHackerNewsUnavailableException(Exception failure)
    {
        _client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>()).ThrowsAsync(failure);

        var act = () => CreateService().GetBestStoriesAsync(1, CancellationToken);

        (await act.Should().ThrowAsync<HackerNewsUnavailableException>())
            .WithInnerException(failure.GetType());
    }

    [Fact]
    public async Task GetBestStoriesAsync_AfterFailure_RetriesOnNextCall()
    {
        _client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("Boom"));
        var failedCall = () => CreateService().GetBestStoriesAsync(1, CancellationToken);
        await failedCall.Should().ThrowAsync<HackerNewsUnavailableException>();
        GivenBestStories((1, 10));

        var stories = await CreateService().GetBestStoriesAsync(1, CancellationToken);

        Titles(stories).Should().Equal("Story 1");
    }

    [Fact]
    public async Task RefreshAsync_ColdCache_PopulatesRanking()
    {
        GivenBestStories((1, 10), (2, 20));

        await CreateService().RefreshAsync(CancellationToken);
        _client.ClearReceivedCalls();
        var stories = await CreateService().GetBestStoriesAsync(2, CancellationToken);

        Titles(stories).Should().Equal("Story 2", "Story 1");
        _client.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshAsync_WarmCache_OverwritesWithFreshData()
    {
        GivenBestStories((1, 10), (2, 20));
        await CreateService().GetBestStoriesAsync(2, CancellationToken);
        GivenBestStories((1, 50), (2, 20), (3, 30));

        await CreateService().RefreshAsync(CancellationToken);
        var stories = await CreateService().GetBestStoriesAsync(3, CancellationToken);

        stories.Select(story => (story.Title, story.Score)).Should()
            .Equal(("Story 1", 50), ("Story 3", 30), ("Story 2", 20));
    }

    [Fact]
    public async Task RefreshAsync_ItemFails_UsesCachedCopyOfItem()
    {
        GivenBestStories((1, 10), (2, 20));
        await CreateService().GetBestStoriesAsync(2, CancellationToken);
        GivenItem(1, 15);
        _client.GetItemAsync(2, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("Boom"));

        await CreateService().RefreshAsync(CancellationToken);
        var stories = await CreateService().GetBestStoriesAsync(2, CancellationToken);

        stories.Select(story => (story.Title, story.Score)).Should().Equal(("Story 2", 20), ("Story 1", 15));
    }

    [Fact]
    public async Task RefreshAsync_BestStoryIdsFail_ThrowsAndKeepsLastRanking()
    {
        GivenBestStories((1, 10), (2, 20));
        await CreateService().GetBestStoriesAsync(2, CancellationToken);
        _client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("Boom"));

        var act = () => CreateService().RefreshAsync(CancellationToken);

        await act.Should().ThrowAsync<HackerNewsUnavailableException>();
        var stories = await CreateService().GetBestStoriesAsync(2, CancellationToken);
        Titles(stories).Should().Equal("Story 2", "Story 1");
    }

    [Fact]
    public async Task RefreshAsync_AllItemsFailWithoutCachedCopies_ThrowsAndKeepsLastRanking()
    {
        GivenBestStories((1, 10));
        await CreateService().GetBestStoriesAsync(1, CancellationToken);
        _client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>()).Returns([7L, 8L]);
        _client.GetItemAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Boom"));

        var act = () => CreateService().RefreshAsync(CancellationToken);

        await act.Should().ThrowAsync<HackerNewsUnavailableException>();
        var stories = await CreateService().GetBestStoriesAsync(1, CancellationToken);
        Titles(stories).Should().Equal("Story 1");
    }

    [Fact]
    public async Task RefreshAsync_Canceled_ThrowsOperationCanceledException()
    {
        GivenBestStories((1, 10));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _client.GetBestStoryIdsAsync(cancellation.Token).ThrowsAsync(new OperationCanceledException(cancellation.Token));

        var act = () => CreateService().RefreshAsync(cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
