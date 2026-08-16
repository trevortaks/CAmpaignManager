namespace CampaignManager.Sdk.Tests.Infrastructure;

/// <summary>Scriptable fake transport for testing typed HttpClient code. Records every request it
/// sees and replays responses from a queue (or a single canned response repeated for every call).</summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>>? _responses;
    private readonly Func<HttpRequestMessage, HttpResponseMessage>? _staticResponder;

    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string?> RequestBodies { get; } = [];

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _staticResponder = responder;
    }

    public StubHttpMessageHandler(IEnumerable<Func<HttpRequestMessage, HttpResponseMessage>> responses)
    {
        _responses = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(responses);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken));

        var responder = _responses is not null ? _responses.Dequeue() : _staticResponder!;
        return responder(request);
    }
}
