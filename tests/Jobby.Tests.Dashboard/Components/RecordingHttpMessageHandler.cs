using System.Net;
using System.Net.Http.Json;

namespace Jobby.Tests.Dashboard.Components;

public sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, object?> router) : HttpMessageHandler
{
    private readonly List<HttpRequestMessage> _requests = [];
    private readonly object _gate = new();

    public IReadOnlyList<HttpRequestMessage> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    public IReadOnlyList<HttpRequestMessage> RequestsTo(string absolutePath)
    {
        lock (_gate)
        {
            return _requests.Where(r => r.RequestUri!.AbsolutePath == absolutePath).ToList();
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _requests.Add(request);
        }

        var body = router(request);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(body),
        });
    }
}