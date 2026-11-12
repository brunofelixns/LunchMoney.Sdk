# SDK client factory

The requested API is a factory that returns a configured `LunchMoneyClient` so
background services do not inject options, construct adapters, or create DI scopes
just to obtain an SDK client.

## Approach

Add public `ILunchMoneyClientFactory.CreateClient()` and an internal singleton
implementation. It holds `IHttpClientFactory` and validated `IOptions`, not an
HTTP or SDK client. Each call requests the existing named HTTP client and uses
the custom partial constructor. `AddLunchMoney` registers the factory alongside
the existing typed client. This keeps URL/token configuration and middleware in
one place while preserving direct constructor injection for ordinary services.

The earlier alternatives (scope-per-operation, or injecting IHttpClientFactory
and IOptions directly) remain valid but repeat plumbing at each worker. The
requested factory encapsulates that plumbing in the SDK.

Keep the HTTP name `LunchMoney`, both registration overloads, validation, custom
HTTP settings, Kiota handlers and host-restricted authentication. Keep all code
outside `Generated`. Do not introduce multi-account settings, hot reload, a
second HTTP pipeline, or new disposal/ownership semantics. Clients are short-lived
per operation; the factory may live for the application lifetime.
