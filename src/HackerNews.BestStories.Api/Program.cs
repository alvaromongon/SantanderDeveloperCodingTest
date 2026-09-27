using HackerNews.BestStories.Api.Apis;
using HackerNews.BestStories.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddHackerNewsClient();
builder.AddBestStoriesService();
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddOpenApi();

var app = builder.Build();

// Binding failures (missing or malformed count) throw in Development; keep their 400 status.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception =>
        exception is BadHttpRequestException badRequest ? badRequest.StatusCode : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();

app.MapOpenApi();
app.MapBestStoriesApi();

await app.RunAsync();
