using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using HackerNews.BestStories.Api.Models;
using HackerNews.BestStories.Api.Services;

using Microsoft.AspNetCore.Http.HttpResults;

namespace HackerNews.BestStories.Api.Apis;

internal static class BestStoriesApi
{
    /// <summary>
    /// Maps <c>GET /api/stories/best?count={n}</c>.
    /// </summary>
    public static RouteGroupBuilder MapBestStoriesApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/stories").WithTags("Stories");

        api.MapGet("/best", GetBestStoriesAsync)
            .WithName("GetBestStories")
            .WithSummary("Gets the best Hacker News stories.")
            .WithDescription(
                "Returns the best `count` Hacker News stories ordered by score descending. " +
                "When `count` exceeds the available best stories (at most 200), all of them are returned.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return api;
    }

    public static async Task<Results<Ok<IReadOnlyList<StoryResponse>>, ProblemHttpResult>> GetBestStoriesAsync(
        [Description("Number of stories to return; at least 1.")][Range(1, int.MaxValue)] int count,
        IBestStoriesService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return TypedResults.Ok(await service.GetBestStoriesAsync(count, cancellationToken));
        }
        catch (HackerNewsUnavailableException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Hacker News is unavailable",
                detail: "The best stories could not be retrieved from Hacker News. Try again later.");
        }
    }
}
