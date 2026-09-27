using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HackerNews.BestStories.Api.ComponentTests;

public sealed class ProgramTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    // The cache refresher calls Hacker News on start: point it to a closed local port so these
    // tests never reach the real API.
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(builder =>
        builder.UseSetting("HackerNews:BaseAddress", "http://127.0.0.1:9/"));

    [Fact]
    public async Task GetOpenApiDocument_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
