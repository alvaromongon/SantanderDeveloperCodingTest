using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

namespace HackerNews.BestStories.Api.ComponentTests;

public sealed class ProgramTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetOpenApiDocument_ReturnsOk()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
