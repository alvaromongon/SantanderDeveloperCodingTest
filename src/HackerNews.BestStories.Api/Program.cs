using HackerNews.BestStories.Api.Apis;
using HackerNews.BestStories.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddHackerNewsClient();
builder.AddBestStoriesService();
builder.AddBestStoriesHealthChecks();
builder.AddRequestConcurrencyLimiter();
builder.Services.AddProblemDetails();
// Binding failures reach the endpoint filters instead of throwing, in every environment.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
// Also turns the empty 429 of the rate limiter into a ProblemDetails.
app.UseStatusCodePages();
app.UseRateLimiter();

app.MapHealthCheckEndpoints();
app.MapOpenApi();
app.MapBestStoriesApi();

await app.RunAsync();
