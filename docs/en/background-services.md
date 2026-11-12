[Documentation](../README.md) · **English** · [Español](../es/background-services.md)

# Background services

`AddLunchMoney` also registers `ILunchMoneyClientFactory` as a singleton. Inject
the factory into singleton services, including a `BackgroundService`, and call
`CreateClient()` for each operation. Each call creates a new configured SDK client
through `IHttpClientFactory`, using the same options, HTTP customizations, Kiota
handlers and host-restricted authentication as direct typed injection. The
factory does not retain HTTP or SDK clients.

## Registration

For a standalone Worker application, use this `Program.cs`. See
[Configuration](configuration.md) for the same `LunchMoney` settings and token setup:

```csharp
using LunchMoney.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddLunchMoney(builder.Configuration.GetSection("LunchMoney"))
    .ConfigureHttpClient(http => http.Timeout = TimeSpan.FromSeconds(30));

builder.Services.AddHostedService<LunchMoneyWorker>();

await builder.Build().RunAsync();
```

## Worker with failure recovery

In `LunchMoneyWorker.cs`, inject the SDK factory and create the client inside
each iteration. This read-only polling example logs recoverable failures,
waits before trying again, and exits normally when the host cancels its token:

```csharp
using LunchMoney.Sdk;
using System.Net.Http.Headers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions;

public sealed class LunchMoneyWorker(
    ILunchMoneyClientFactory clientFactory,
    ILogger<LunchMoneyWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RecoveryDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var nextRunDelay = TimeSpan.FromMinutes(5);
                try
                {
                    var client = clientFactory.CreateClient();
                    var response = await client.Categories.GetAsync(
                        cancellationToken: stoppingToken);

                    logger.LogInformation("Lunch Money returned {CategoryCount} categories",
                        response?.Categories?.Count ?? 0);
                }
                catch (ApiException error) when (IsRecoverable(error))
                {
                    nextRunDelay = GetRetryDelay(error);
                    logger.LogWarning("Lunch Money returned HTTP {Status}; retrying in {Delay}.",
                        error.ResponseStatusCode, nextRunDelay);
                }
                catch (AggregateException error) when (IsRecoverableRetryFailure(error))
                {
                    var lastError = (ApiException)error.Flatten().InnerExceptions[^1];
                    nextRunDelay = GetRetryDelay(lastError);
                    logger.LogWarning("Lunch Money retries exhausted (HTTP {Status}); retrying in {Delay}.",
                        lastError.ResponseStatusCode, nextRunDelay);
                }
                catch (HttpRequestException error)
                {
                    nextRunDelay = RecoveryDelay;
                    logger.LogWarning("Lunch Money connection failed ({Error}); retrying in {Delay}.",
                        error.HttpRequestError, nextRunDelay);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    nextRunDelay = RecoveryDelay;
                    logger.LogWarning("Lunch Money request timed out; retrying in {Delay}.", nextRunDelay);
                }

                await Task.Delay(nextRunDelay, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown, including cancellation during HTTP or a retry delay.
        }
    }

    private static bool IsRecoverable(ApiException error) =>
        error.ResponseStatusCode is 408 or 429 or >= 500 and <= 599;

    private static bool IsRecoverableRetryFailure(AggregateException error)
    {
        var failures = error.Flatten().InnerExceptions;
        return failures.Count > 0 && failures.All(failure =>
            failure is ApiException apiError && IsRecoverable(apiError));
    }

    private static TimeSpan GetRetryDelay(ApiException error)
    {
        var header = error.ResponseHeaders.FirstOrDefault(entry =>
            entry.Key.Equals("Retry-After", StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault();
        if (!RetryConditionHeaderValue.TryParse(header, out var retryAfter))
            return RecoveryDelay;

        var requestedDelay = retryAfter.Delta ?? (retryAfter.Date - DateTimeOffset.UtcNow) ?? TimeSpan.Zero;
        return requestedDelay > RecoveryDelay ? requestedDelay : RecoveryDelay;
    }
}
```

The example continues after HTTP `408`, `429`, `5xx`, exhausted transient retries,
network failures and timeouts. It waits at least 30 seconds after a failure and
honors a longer `Retry-After` value (seconds or HTTP date). A successful poll waits
five minutes. These intervals are example application policy, not SDK defaults.

Permanent errors such as `400` or `401`, invalid SDK settings and unexpected
programming failures are allowed to reach the host. This keeps those problems
visible rather than repeatedly treating them as transient. The loop polls with
`GET`; choose retry and idempotency behavior explicitly when adapting it to writes.

## Hosting in ASP.NET Core

To host the worker inside an ASP.NET Core application instead, add
`builder.Services.AddHostedService<LunchMoneyWorker>()` before `builder.Build()`
in that application's `Program.cs`, using the same SDK registration.

The worker does not need options, a client name, adapter construction or an
application DI scope just to use the SDK. Keep the returned client local to the
operation. If the operation also uses scoped application services, such as an
EF Core `DbContext`, resolve those services in a scope as usual.

See [Error handling](error-handling.md) for the exception contracts.
