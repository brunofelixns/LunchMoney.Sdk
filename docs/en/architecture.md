[Documentation](../README.md) · **English** · [Español](../es/architecture.md)

# Architecture

The SDK keeps the generated API, HTTP configuration and application lifetimes
separate. Applications use the generated `LunchMoneyClient` directly; integration
code supplies its transport, authentication and options.

## Why IHttpClientFactory

`AddLunchMoney` uses `AddHttpClient<LunchMoneyClient>` and returns
`IHttpClientBuilder`. This gives applications the usual .NET configuration points
for timeouts, delegating handlers and HTTP transport.

`IHttpClientFactory` creates short-lived HTTP clients while pooling their
underlying handlers. Requesting new clients allows handler rotation and avoids
rebuilding the connection infrastructure for each operation. See Microsoft's
[HTTP client lifetime guidance](https://learn.microsoft.com/dotnet/core/extensions/httpclient-factory#httpclient-lifetime-management).

Registration explicitly attaches Kiota's default middleware to the factory-managed
transport, including retries. Direct injection and the SDK factory share the
same named HTTP configuration, so HTTP customizations apply to both paths.
Options validation and Bearer authentication restricted to the configured host
also live in this integration layer.

## A factory for background services

The typed `LunchMoneyClient` is transient and fits scoped or transient consumers.
A hosted `BackgroundService` is a singleton; capturing a typed client in its
constructor would keep that client's HTTP instance for the worker's lifetime.
Microsoft documents this in
[Avoid typed clients in singleton services](https://learn.microsoft.com/dotnet/core/extensions/httpclient-factory#avoid-typed-clients-in-singleton-services).

`ILunchMoneyClientFactory` provides an SDK-level creation boundary for these
long-lived consumers. Its singleton implementation holds `IHttpClientFactory`
and `IOptions<LunchMoneyOptions>`, without caching HTTP or SDK client instances.
Each `CreateClient()` requests the named HTTP client and constructs a configured
`LunchMoneyClient` through the same partial constructor used by typed injection.

Workers can therefore request a client per operation without repeating options,
authentication or adapter construction. They do not need a DI scope solely to
obtain the SDK. Operations that also use scoped application services, such as an
EF Core `DbContext`, still need an appropriate scope for those services.
See the complete [background service example](background-services.md).

## Keep custom code outside Generated

`kiota update --clean-output` replaces the contents of `Generated`. Custom
integration code stays outside that directory so regeneration preserves it:

| Boundary | Files | Responsibility |
| :--- | :--- | :--- |
| Generated API | [Generated/](../../Generated/) | Request builders, API models and the generated client constructor. |
| Registration and settings | [LunchMoneyServiceCollectionExtensions.cs](../../LunchMoneyServiceCollectionExtensions.cs), [LunchMoneyOptions.cs](../../LunchMoneyOptions.cs) | DI registration, HTTP pipeline and validated connection settings. |
| SDK creation | [ILunchMoneyClientFactory.cs](../../ILunchMoneyClientFactory.cs), [LunchMoneyClientFactory.cs](../../LunchMoneyClientFactory.cs) | Create a configured SDK client for each operation. |
| Adapter integration | [LunchMoneyClient.Extensions.cs](../../LunchMoneyClient.Extensions.cs) | Partial constructor accepting the HTTP client and options. |

The partial constructor creates the Kiota request adapter and calls the generated
`IRequestAdapter` constructor. `ActivatorUtilitiesConstructor` selects the custom
constructor for typed injection, while the generated constructor remains
available for manual integration.

When regenerating, preserve the client class name and namespace, review the API
diff and run the tests. See [Development](development.md) for the exact command
and [Configuration](configuration.md) for the options contract.
