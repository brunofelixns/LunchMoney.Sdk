[Documentation](../README.md) · **English** · [Español](../es/error-handling.md)

# Error handling

The SDK propagates failures to the caller's `await`. Direct injection and the
client factory use the same exception behavior; registration does not install an
application-wide exception handler.

| Failure | What the caller receives |
| :--- | :--- |
| HTTP error with a generated mapping | `ErrorResponseObject`, derived from `ApiException`, with status, message, headers and `Errors`. Mappings depend on the endpoint. |
| HTTP error without a mapping | `ApiException`, with `ResponseStatusCode` and `ResponseHeaders`. |
| Kiota retry budget exhausted | `AggregateException` containing the HTTP failures as `ApiException` entries. |
| Connection failure | `HttpRequestException`. |
| HTTP timeout or caller cancellation | `OperationCanceledException`; check the caller token to distinguish shutdown from timeout. |
| Missing or invalid SDK settings | `OptionsValidationException` during host startup or client creation. |

## Retries

The default Kiota retry handler retries `429`, `503` and `504`. Exhaustion in the
current Kiota version produces an aggregate, so catching only `ApiException` is
not enough for a resilient worker. Inner retry error messages can include response
bodies; the example logs status and timing rather than dumping those exceptions.

## Where to handle failures

In ASP.NET Core, handle failures in the consuming service or an application
exception handler. In a `BackgroundService`, catch recoverable failures inside the
loop as shown in [Background services](background-services.md). An unhandled background exception stops the host by default;
setting `BackgroundServiceExceptionBehavior.Ignore` does not restart a failed loop.

See [Kiota error handling](https://learn.microsoft.com/openapi/kiota/errors),
[Kiota's retry handler](https://github.com/microsoft/kiota-dotnet/blob/main/src/http/httpClient/Middleware/RetryHandler.cs),
and [.NET background exception behavior](https://learn.microsoft.com/dotnet/core/compatibility/core-libraries/6.0/hosting-exception-handling).
