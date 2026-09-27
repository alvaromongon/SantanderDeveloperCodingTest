using System.ComponentModel;
using System.Globalization;

using HackerNews.BestStories.Api.Models;
using HackerNews.BestStories.Api.Services;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace HackerNews.BestStories.Api.Apis;

internal static class BestStoriesApi
{
    private const string CountParameter = "count";
    private const string CountRequiredError = "The count query parameter is required.";
    private const string CountInvalidError = "count must be an integer greater than or equal to 1.";

    /// <summary>
    /// Maps <c>GET /api/stories/best?count={n}</c>.
    /// </summary>
    /// <remarks>
    /// Requires <see cref="RouteHandlerOptions.ThrowOnBadRequest"/> to be <see langword="false"/>, so a
    /// missing or malformed <c>count</c> reaches the validation filter in every environment.
    /// </remarks>
    public static RouteGroupBuilder MapBestStoriesApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/stories").WithTags("Stories");

        api.MapGet("/best", GetBestStoriesAsync)
            .WithName("GetBestStories")
            .WithSummary("Gets the best Hacker News stories.")
            .WithDescription("Returns the best `count` Hacker News stories ordered by score descending.")
            .AddEndpointFilter(ValidateCountAsync)
            .AddOpenApiOperationTransformer(DocumentCountMinimum)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return api;
    }

    public static async Task<Results<Ok<IReadOnlyList<StoryResponse>>, ProblemHttpResult>> GetBestStoriesAsync(
        [Description(
            "Number of stories to return; at least 1. There is no upper limit: values above the number of " +
            "best stories provided by Hacker News (currently at most 200) return all of them.")]
        int count,
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

    // Validates the raw query value, so binding failures (missing, not an integer, out of the int
    // range) and values lower than 1 get a precise error; the handler only runs with a valid count.
    private static async ValueTask<object?> ValidateCountAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var value = context.HttpContext.Request.Query[CountParameter].ToString();

        var error = string.IsNullOrEmpty(value) ? CountRequiredError
            : int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count >= 1 ? null
            : CountInvalidError;

        return error is null
            ? await next(context)
            : TypedResults.ValidationProblem(new Dictionary<string, string[]> { [CountParameter] = [error] });
    }

    // Only a minimum: the number of stories returned is bounded by Hacker News, not by the API.
    private static Task DocumentCountMinimum(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (operation.Parameters?.SingleOrDefault(parameter => parameter.Name == CountParameter)?.Schema
            is OpenApiSchema schema)
        {
            schema.Minimum = "1";
        }

        return Task.CompletedTask;
    }
}
