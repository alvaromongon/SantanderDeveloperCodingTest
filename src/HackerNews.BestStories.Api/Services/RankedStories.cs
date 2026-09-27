using System.Collections.Immutable;
using System.ComponentModel;

using HackerNews.BestStories.Api.Models;

namespace HackerNews.BestStories.Api.Services;

/// <summary>
/// Stories ordered by score descending, as cached for requests.
/// </summary>
/// <remarks>
/// Sealed and marked immutable so <c>HybridCache</c> returns the cached instance instead of
/// deserializing a copy on every request.
/// </remarks>
[ImmutableObject(true)]
internal sealed record RankedStories(ImmutableArray<StoryResponse> Stories)
{
    public IReadOnlyList<StoryResponse> Take(int count) =>
        count >= Stories.Length ? Stories : Stories.Slice(0, count);
}
