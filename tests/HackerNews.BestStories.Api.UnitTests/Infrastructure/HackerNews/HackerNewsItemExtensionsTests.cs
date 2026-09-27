using HackerNews.BestStories.Api.Infrastructure.HackerNews;

namespace HackerNews.BestStories.Api.UnitTests.Infrastructure.HackerNews;

public sealed class HackerNewsItemExtensionsTests
{
    private static HackerNewsItem CreateStory() => new()
    {
        Id = 21233041,
        Type = "story",
        By = "ismaildonmez",
        Time = 1570887781,
        Title = "A uBlock Origin update was rejected from the Chrome Web Store",
        Url = "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
        Score = 1716,
        Descendants = 572,
    };

    [Fact]
    public void ToStoryResponse_Story_MapsAllFields()
    {
        var story = CreateStory().ToStoryResponse();

        story.Should().Be(new Api.Models.StoryResponse(
            Title: "A uBlock Origin update was rejected from the Chrome Web Store",
            Uri: new Uri("https://github.com/uBlockOrigin/uBlock-issues/issues/745"),
            PostedBy: "ismaildonmez",
            Time: new DateTimeOffset(2019, 10, 12, 13, 43, 1, TimeSpan.Zero),
            Score: 1716,
            CommentCount: 572));
    }

    [Fact]
    public void ToStoryResponse_Story_ConvertsUnixTimeToUtc()
    {
        var story = CreateStory().ToStoryResponse();

        story!.Time.Offset.Should().Be(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a valid url")]
    public void ToStoryResponse_MissingOrInvalidUrl_FallsBackToHackerNewsItemPage(string? url)
    {
        var item = CreateStory() with { Id = 121003, Url = url };

        var story = item.ToStoryResponse();

        story!.Uri.Should().Be(new Uri("https://news.ycombinator.com/item?id=121003"));
    }

    [Fact]
    public void ToStoryResponse_MissingDescendants_ReturnsZeroCommentCount()
    {
        var item = CreateStory() with { Descendants = null };

        var story = item.ToStoryResponse();

        story!.CommentCount.Should().Be(0);
    }

    [Fact]
    public void ToStoryResponse_MissingTitleAndAuthor_ReturnsEmptyStrings()
    {
        var item = CreateStory() with { Title = null, By = null };

        var story = item.ToStoryResponse();

        story!.Title.Should().BeEmpty();
        story.PostedBy.Should().BeEmpty();
    }

    [Fact]
    public void ToStoryResponse_DeletedItem_ReturnsNull()
    {
        var item = CreateStory() with { Deleted = true };

        item.ToStoryResponse().Should().BeNull();
    }

    [Fact]
    public void ToStoryResponse_DeadItem_ReturnsNull()
    {
        var item = CreateStory() with { Dead = true };

        item.ToStoryResponse().Should().BeNull();
    }

    [Theory]
    [InlineData("comment")]
    [InlineData("job")]
    [InlineData("poll")]
    [InlineData("pollopt")]
    [InlineData(null)]
    public void ToStoryResponse_NonStoryItem_ReturnsNull(string? type)
    {
        var item = CreateStory() with { Type = type };

        item.ToStoryResponse().Should().BeNull();
    }
}
