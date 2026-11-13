using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace LunchMoney.Sdk.MockTests;

public sealed class OfficialMockFixture : IDisposable
{
    internal const string BaseUrl = "https://mock.lunchmoney.dev/v2";
    private readonly ServiceProvider provider;
    private readonly MockTrafficLog traffic = new();

    public OfficialMockFixture()
    {
        var services = new ServiceCollection();
        services.AddLunchMoney(options =>
        {
            options.BaseUrl = BaseUrl;
            options.Token = $"sdk-mock-{Guid.NewGuid():N}";
        })
        .ConfigureHttpClient(http => http.Timeout = TimeSpan.FromSeconds(90))
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = KiotaClientFactory.GetDefaultHttpMessageHandler();
            if (handler is HttpClientHandler http) http.AllowAutoRedirect = false;
            if (handler is SocketsHttpHandler sockets) sockets.AllowAutoRedirect = false;
            return handler;
        })
        .AddHttpMessageHandler(() => new MockTransportObserver(traffic));
        provider = services.BuildServiceProvider();
    }

    internal IReadOnlyList<ObservedRequest> Requests => traffic.Requests;

    internal LunchMoneyClient CreateClient()
    {
        traffic.Requests.Clear();
        return provider.GetRequiredService<ILunchMoneyClientFactory>().CreateClient();
    }

    public void Dispose() => provider.Dispose();

    internal static void EnsureMockDestination(Uri? uri)
    {
        if (uri is not { IsAbsoluteUri: true, Scheme: "https", Host: "mock.lunchmoney.dev", Port: 443 }
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !uri.AbsolutePath.StartsWith("/v2/", StringComparison.Ordinal))
            throw new InvalidOperationException("Mock tests may only contact https://mock.lunchmoney.dev/v2/.");
    }

    private sealed class MockTrafficLog
    {
        public long LastRequest;
        public List<ObservedRequest> Requests { get; } = [];
    }

    private sealed class MockTransportObserver(MockTrafficLog traffic) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            EnsureMockDestination(request.RequestUri);
            if (traffic.LastRequest != 0)
            {
                var remaining = TimeSpan.FromSeconds(4) - Stopwatch.GetElapsedTime(traffic.LastRequest);
                if (remaining > TimeSpan.Zero) await Task.Delay(remaining, token);
            }
            traffic.LastRequest = Stopwatch.GetTimestamp();
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(token);
            var response = await base.SendAsync(request, token);
            var content = response.Content is null ? "" : await response.Content.ReadAsStringAsync(token);
            traffic.Requests.Add(new(request.Method.Method, request.RequestUri!.AbsolutePath, request.RequestUri.Query,
                request.Content?.Headers.ContentType?.MediaType, body, response.StatusCode, content));
            return response;
        }
    }
}

internal sealed record ObservedRequest(string Method, string Path, string Query, string? ContentType,
    string? RequestBody, HttpStatusCode StatusCode, string ResponseBody);
