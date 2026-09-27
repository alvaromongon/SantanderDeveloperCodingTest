using System.Text.Json;
using System.Text.Json.Serialization;

namespace HackerNews.BestStories.Api.Infrastructure.HackerNews;

/// <summary>
/// Source-generated JSON metadata for the Hacker News API payloads (camelCase, web defaults).
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(long[]))]
[JsonSerializable(typeof(HackerNewsItem))]
internal sealed partial class HackerNewsJsonContext : JsonSerializerContext;
