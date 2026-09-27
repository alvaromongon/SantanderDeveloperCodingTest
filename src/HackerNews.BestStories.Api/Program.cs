using HackerNews.BestStories.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddHackerNewsClient();
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();

await app.RunAsync();
