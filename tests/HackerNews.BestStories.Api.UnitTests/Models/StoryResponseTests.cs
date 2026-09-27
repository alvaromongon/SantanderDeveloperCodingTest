using System.Text.Json;

using HackerNews.BestStories.Api.Models;

namespace HackerNews.BestStories.Api.UnitTests.Models;

public sealed class StoryResponseTests
{
    [Fact]
    public void Serialize_WithWebDefaults_ProducesExpectedContract()
    {
        var story = new StoryResponse(
            Title: "A uBlock Origin update was rejected from the Chrome Web Store",
            Uri: new Uri("https://github.com/uBlockOrigin/uBlock-issues/issues/745"),
            PostedBy: "ismaildonmez",
            Time: new DateTimeOffset(2019, 10, 12, 13, 43, 1, TimeSpan.Zero),
            Score: 1716,
            CommentCount: 572);

        var json = JsonSerializer.Serialize(story, JsonSerializerOptions.Web);

        json.Should().Be(
            """{"title":"A uBlock Origin update was rejected from the Chrome Web Store","uri":"https://github.com/uBlockOrigin/uBlock-issues/issues/745","postedBy":"ismaildonmez","time":"2019-10-12T13:43:01+00:00","score":1716,"commentCount":572}""");
    }
}
