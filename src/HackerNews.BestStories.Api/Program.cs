using HackerNews.BestStories.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddHackerNewsClient();
builder.AddBestStoriesService();
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();

await app.RunAsync();
