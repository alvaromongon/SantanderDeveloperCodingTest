using System.Net;
using System.Text;

namespace HackerNews.BestStories.Api.UnitTests.TestDoubles;

/// <summary>
/// Fake <see cref="HttpMessageHandler"/> that answers requests by relative path and records them,
/// so HTTP clients can be tested without calling the network.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _responses = [];

    public List<Uri> Requests { get; } = [];

    public StubHttpMessageHandler RespondWithJson(string path, string json) =>
        RespondWith(path, HttpStatusCode.OK, json);

    public StubHttpMessageHandler RespondWith(string path, HttpStatusCode statusCode, string content = "")
    {
        _responses[path] = () => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        };
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        Requests.Add(uri);

        var response = _responses.TryGetValue(uri.AbsolutePath, out var createResponse)
            ? createResponse()
            : new HttpResponseMessage(HttpStatusCode.NotFound);

        return Task.FromResult(response);
    }
}
