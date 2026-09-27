using HackerNews.BestStories.Api.Apis;
using HackerNews.BestStories.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddHackerNewsClient();
builder.AddBestStoriesService();
builder.Services.AddProblemDetails();
// Binding failures reach the endpoint filters instead of throwing, in every environment.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.MapBestStoriesApi();

await app.RunAsync();
