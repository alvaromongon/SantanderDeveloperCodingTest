using System.Net;
using System.Text.Json;

using HackerNews.BestStories.Api.Infrastructure.HackerNews;
using HackerNews.BestStories.Api.UnitTests.TestDoubles;

namespace HackerNews.BestStories.Api.UnitTests.Infrastructure.HackerNews;

public sealed class HackerNewsClientTests : IDisposable
{
    private readonly StubHttpMessageHandler _handler = new();
    private readonly HttpClient _httpClient;
    private readonly HackerNewsClient _client;

    public HackerNewsClientTests()
    {
        _httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://hacker-news.test/v0/") };
        _client = new HackerNewsClient(_httpClient);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetBestStoryIdsAsync_ValidResponse_ReturnsIdsInOriginalOrder()
    {
        _handler.RespondWithJson("/v0/beststories.json", "[21233041, 8863, 121003]");

        var ids = await _client.GetBestStoryIdsAsync(CancellationToken);

        ids.Should().Equal(21233041, 8863, 121003);
    }

    [Fact]
    public async Task GetBestStoryIdsAsync_NullResponse_ReturnsEmptyList()
    {
        _handler.RespondWithJson("/v0/beststories.json", "null");

        var ids = await _client.GetBestStoryIdsAsync(CancellationToken);

        ids.Should().BeEmpty();
    }

    [Fact]
    public async Task GetItemAsync_ExistingItem_DeserializesAllFields()
    {
        _handler.RespondWithJson("/v0/item/8863.json", """
            {
              "by": "dhouston",
              "descendants": 71,
              "id": 8863,
              "kids": [8952, 9224],
              "score": 111,
              "time": 1175714200,
              "title": "My YC app: Dropbox - Throw away your USB drive",
              "type": "story",
              "url": "http://www.getdropbox.com/u/2/screencast.html"
            }
            """);

        var item = await _client.GetItemAsync(8863, CancellationToken);

        item.Should().Be(new HackerNewsItem
        {
            Id = 8863,
            Type = "story",
            By = "dhouston",
            Time = 1175714200,
            Title = "My YC app: Dropbox - Throw away your USB drive",
            Url = "http://www.getdropbox.com/u/2/screencast.html",
            Score = 111,
            Descendants = 71,
        });
    }

    [Fact]
    public async Task GetItemAsync_DeletedAndDeadFlags_AreDeserialized()
    {
        _handler.RespondWithJson("/v0/item/42.json", """{ "id": 42, "deleted": true, "dead": true }""");

        var item = await _client.GetItemAsync(42, CancellationToken);

        item!.Deleted.Should().BeTrue();
        item.Dead.Should().BeTrue();
    }

    [Fact]
    public async Task GetItemAsync_NonExistentItem_ReturnsNull()
    {
        _handler.RespondWithJson("/v0/item/999999999.json", "null");

        var item = await _client.GetItemAsync(999999999, CancellationToken);

        item.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetBestStoryIdsAsync_ErrorStatusCode_ThrowsHttpRequestException(HttpStatusCode statusCode)
    {
        _handler.RespondWith("/v0/beststories.json", statusCode);

        var act = () => _client.GetBestStoryIdsAsync(CancellationToken);

        (await act.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(statusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetItemAsync_ErrorStatusCode_ThrowsHttpRequestException(HttpStatusCode statusCode)
    {
        _handler.RespondWith("/v0/item/8863.json", statusCode);

        var act = () => _client.GetItemAsync(8863, CancellationToken);

        (await act.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(statusCode);
    }

    [Fact]
    public async Task GetBestStoryIdsAsync_MalformedJson_ThrowsJsonException()
    {
        _handler.RespondWithJson("/v0/beststories.json", "[1, 2,");

        var act = () => _client.GetBestStoryIdsAsync(CancellationToken);

        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task GetItemAsync_UnexpectedJsonShape_ThrowsJsonException()
    {
        _handler.RespondWithJson("/v0/item/8863.json", """{ "id": "not a number" }""");

        var act = () => _client.GetItemAsync(8863, CancellationToken);

        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task GetItemAsync_RequestsItemRelativeToBaseAddress()
    {
        _handler.RespondWithJson("/v0/item/8863.json", "null");

        await _client.GetItemAsync(8863, CancellationToken);

        _handler.Requests.Should().ContainSingle()
            .Which.Should().Be(new Uri("https://hacker-news.test/v0/item/8863.json"));
    }
}
