using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LunchMoney.Sdk.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Http.HttpClientLibrary.Middleware.Options;
using Xunit;

namespace LunchMoney.Sdk.Tests;

public sealed class ErrorHandlingTests
{
    private const string ErrorJson =
        "{\"message\":\"Synthetic API failure\",\"errors\":[{\"errMsg\":\"Synthetic validation detail\"}]}";
    private const string SuccessJson = "{\"categories\":[]}";

    public static TheoryData<bool, int> MappedErrors => new()
    {
        { false, 400 }, { true, 400 },
        { false, 401 }, { true, 401 },
        { false, 429 }, { true, 429 },
        { false, 500 }, { true, 500 }
    };

    public static TheoryData<bool, int> TransientErrors => new()
    {
        { false, 429 }, { true, 429 },
        { false, 503 }, { true, 503 },
        { false, 504 }, { true, 504 }
    };

    [Theory]
    [MemberData(nameof(MappedErrors))]
    public async Task MappedHttpErrorsExposeStatusHeadersAndServerDetails(bool useFactory, int status)
    {
        var handler = new ScriptedHandler((request, _, _) =>
            Task.FromResult(JsonResponse(request, status, ErrorJson)));
        using var provider = CreateProvider(handler);
        var client = ResolveClient(provider, useFactory);

        var error = await Assert.ThrowsAsync<ErrorResponseObject>(() =>
            client.Categories.GetAsync(config =>
                config.Options.Add(new RetryHandlerOption { MaxRetry = 0 })));

        Assert.Equal(status, error.ResponseStatusCode);
        Assert.Equal("Synthetic API failure", error.Message);
        Assert.Equal("Synthetic validation detail", Assert.Single(error.Errors!).ErrMsg);
        Assert.Equal("synthetic-request-id", Assert.Single(error.ResponseHeaders["X-Request-Id"]));
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnmappedHttpErrorsPreserveStatusAndHeaders(bool useFactory)
    {
        var handler = new ScriptedHandler((request, _, _) =>
            Task.FromResult(JsonResponse(request, 502, ErrorJson)));
        using var provider = CreateProvider(handler);

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            ResolveClient(provider, useFactory).Categories.GetAsync());

        Assert.Equal(502, error.ResponseStatusCode);
        Assert.Equal("synthetic-request-id", Assert.Single(error.ResponseHeaders["X-Request-Id"]));
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MalformedErrorJsonRetainsHttpContextAndParseFailure(bool useFactory)
    {
        var handler = new ScriptedHandler((request, _, _) =>
            Task.FromResult(JsonResponse(request, 400, "{\"message\":")));
        using var provider = CreateProvider(handler);

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            ResolveClient(provider, useFactory).Categories.GetAsync());

        Assert.Equal(400, error.ResponseStatusCode);
        Assert.IsAssignableFrom<JsonException>(error.InnerException);
        Assert.Equal("synthetic-request-id", Assert.Single(error.ResponseHeaders["X-Request-Id"]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnauthorizedResponsesAreNotRetried(bool useFactory)
    {
        var handler = new ScriptedHandler((request, _, _) =>
            Task.FromResult(JsonResponse(request, 401, ErrorJson)));
        using var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<ErrorResponseObject>(() =>
            ResolveClient(provider, useFactory).Categories.GetAsync());

        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [MemberData(nameof(TransientErrors))]
    public async Task TransientFailuresAreRetriedUntilSuccess(bool useFactory, int status)
    {
        var handler = new ScriptedHandler((request, attempt, _) =>
            Task.FromResult(attempt <= 2
                ? JsonResponse(request, status, ErrorJson, retryAfterSeconds: 0)
                : JsonResponse(request, 200, SuccessJson)));
        using var provider = CreateProvider(handler);

        var response = await ResolveClient(provider, useFactory).Categories.GetAsync(config =>
            config.Options.Add(new RetryHandlerOption { MaxRetry = 2, Delay = 0 }));

        Assert.NotNull(response);
        Assert.Empty(response.Categories!);
        Assert.Equal(3, handler.RequestCount);
    }

    [Theory]
    [MemberData(nameof(TransientErrors))]
    public async Task ExhaustedRetriesExposeEachHttpFailureAndStopAtTheConfiguredLimit(bool useFactory, int status)
    {
        var handler = new ScriptedHandler((request, _, _) =>
            Task.FromResult(JsonResponse(request, status, ErrorJson, retryAfterSeconds: 0)));
        using var provider = CreateProvider(handler);

        var error = await Assert.ThrowsAsync<AggregateException>(() =>
            ResolveClient(provider, useFactory).Categories.GetAsync(config =>
                config.Options.Add(new RetryHandlerOption { MaxRetry = 2, Delay = 0 })));

        Assert.Equal(3, handler.RequestCount);
        Assert.Equal(3, error.InnerExceptions.Count);
        Assert.All(error.InnerExceptions, failure =>
        {
            var apiError = Assert.IsType<ApiException>(failure);
            Assert.Equal(status, apiError.ResponseStatusCode);
            // RetryHandler uses a case-sensitive dictionary with canonicalized header names.
            var requestId = Assert.Single(apiError.ResponseHeaders, header =>
                header.Key.Equals("X-Request-Id", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("synthetic-request-id", Assert.Single(requestId.Value));
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RetryAfterSecondsAndHttpDatesDelayTheNextAttempt(bool useFactory, bool useHttpDate)
    {
        long firstAttemptTime = 0;
        TimeSpan elapsedBeforeRetry = TimeSpan.Zero;
        var handler = new ScriptedHandler((request, attempt, _) =>
        {
            if (attempt == 1)
            {
                var response = JsonResponse(request, 429, ErrorJson);
                response.Headers.RetryAfter = useHttpDate
                    ? new RetryConditionHeaderValue(DateTimeOffset.FromUnixTimeSeconds(
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 2))
                    : new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
                firstAttemptTime = Stopwatch.GetTimestamp();
                return Task.FromResult(response);
            }

            elapsedBeforeRetry = Stopwatch.GetElapsedTime(firstAttemptTime);
            return Task.FromResult(JsonResponse(request, 200, SuccessJson));
        });
        using var provider = CreateProvider(handler);

        var response = await ResolveClient(provider, useFactory).Categories.GetAsync(config =>
            config.Options.Add(new RetryHandlerOption { MaxRetry = 1, Delay = 0 }))
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotNull(response);
        Assert.Equal(2, handler.RequestCount);
        // A future HTTP date rounded to seconds still gives at least one second.
        // Allow timer granularity; do not impose an upper timing bound on busy CI.
        Assert.True(elapsedBeforeRetry >= TimeSpan.FromMilliseconds(750),
            $"Retry-After was bypassed: retry occurred after {elapsedBeforeRetry}.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NetworkFailuresReachTheCallerWithoutBeingConvertedToHttpErrors(bool useFactory)
    {
        var networkError = new HttpRequestException("Synthetic connection failure");
        var handler = new ScriptedHandler((_, _, _) => throw networkError);
        using var provider = CreateProvider(handler);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            ResolveClient(provider, useFactory).Categories.GetAsync());

        Assert.Same(networkError, error);
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationBeforeSendingDoesNotReachTheTransport(bool useFactory)
    {
        var handler = new ScriptedHandler((request, _, _) =>
            Task.FromResult(JsonResponse(request, 200, SuccessJson)));
        using var provider = CreateProvider(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ResolveClient(provider, useFactory).Categories.GetAsync(cancellationToken: cancellation.Token));

        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringHttpReachesTheTransportAndCaller(bool useFactory)
    {
        var handler = PendingHandler();
        using var provider = CreateProvider(handler);
        using var cancellation = new CancellationTokenSource();
        var operation = ResolveClient(provider, useFactory).Categories.GetAsync(
            cancellationToken: cancellation.Token);
        await handler.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            operation.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(handler.LastCancellationToken.IsCancellationRequested);
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringRetryAfterStopsWithoutAnotherAttempt(bool useFactory)
    {
        var handler = new ScriptedHandler((request, _, _) =>
            Task.FromResult(JsonResponse(request, 429, ErrorJson, retryAfterSeconds: 30)));
        using var provider = CreateProvider(handler);
        using var cancellation = new CancellationTokenSource();
        var operation = ResolveClient(provider, useFactory).Categories.GetAsync(config =>
            config.Options.Add(new RetryHandlerOption { MaxRetry = 1, Delay = 0 }), cancellation.Token);

        Assert.Equal(1, handler.RequestCount);
        Assert.False(operation.IsCompleted);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            operation.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpTimeoutCancelsTheTransportWithoutCancellingTheCallerToken(bool useFactory)
    {
        var handler = PendingHandler();
        using var provider = CreateProvider(handler, TimeSpan.FromMilliseconds(200));
        using var cancellation = new CancellationTokenSource();

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ResolveClient(provider, useFactory).Categories.GetAsync(cancellationToken: cancellation.Token)
                .WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.IsType<TimeoutException>(error.InnerException);
        Assert.False(cancellation.IsCancellationRequested);
        Assert.True(handler.LastCancellationToken.IsCancellationRequested);
        Assert.Equal(1, handler.RequestCount);
    }

    private static ServiceProvider CreateProvider(HttpMessageHandler handler, TimeSpan? timeout = null)
    {
        var services = new ServiceCollection();
        services.AddLunchMoney(options => options.Token = "synthetic-error-test-token")
            .ConfigureHttpClient(http => http.Timeout = timeout ?? TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    private static LunchMoneyClient ResolveClient(IServiceProvider provider, bool useFactory) =>
        useFactory
            ? provider.GetRequiredService<ILunchMoneyClientFactory>().CreateClient()
            : provider.GetRequiredService<LunchMoneyClient>();

    private static ScriptedHandler PendingHandler() => new(async (request, _, cancellationToken) =>
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return JsonResponse(request, 200, SuccessJson);
    });

    private static HttpResponseMessage JsonResponse(
        HttpRequestMessage request, int status, string json, int? retryAfterSeconds = null)
    {
        var response = new HttpResponseMessage((HttpStatusCode)status)
        {
            RequestMessage = request,
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        response.Headers.Add("X-Request-Id", "synthetic-request-id");
        if (retryAfterSeconds is { } seconds)
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
        return response;
    }

    private sealed class ScriptedHandler(
        Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        private int requestCount;
        public int RequestCount => Volatile.Read(ref requestCount);
        public CancellationToken LastCancellationToken { get; private set; }
        public TaskCompletionSource<bool> RequestStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Honor cancellation before simulated I/O, as a real network transport does.
            cancellationToken.ThrowIfCancellationRequested();
            var attempt = Interlocked.Increment(ref requestCount);
            LastCancellationToken = cancellationToken;
            RequestStarted.TrySetResult(true);
            return send(request, attempt, cancellationToken);
        }
    }
}
