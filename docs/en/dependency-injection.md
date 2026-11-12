[Documentation](../README.md) · **English** · [Español](../es/dependency-injection.md)

# Dependency injection

Choose the dependency that fits the lifetime of your consumer. Both use the
same [registration](getting-started.md) and [connection settings](configuration.md).

| Consumer | Inject | Usage |
| :--- | :--- | :--- |
| A scoped or transient application service | `LunchMoneyClient` | Call the SDK directly. |
| A singleton, scheduled job or `BackgroundService` | `ILunchMoneyClientFactory` | Call `CreateClient()` for each operation. |

## Direct client injection

Use direct client injection in scoped or transient services. This complete
ASP.NET Core `Program.cs` registers the SDK and a service, then exposes an endpoint
that uses that service:

```csharp
using LunchMoney.Sdk;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddLunchMoney(builder.Configuration.GetSection("LunchMoney"))
    .ConfigureHttpClient(http => http.Timeout = TimeSpan.FromSeconds(30));

builder.Services.AddScoped<CategoryService>();

var app = builder.Build();

app.MapGet("/categories/count", async (
    CategoryService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.CountAsync(cancellationToken)));

app.Run();
```

In `CategoryService.cs`, inject the configured SDK client through its constructor:

```csharp
using LunchMoney.Sdk;

public sealed class CategoryService(LunchMoneyClient client)
{
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var response = await client.Categories.GetAsync(
            cancellationToken: cancellationToken);
        return response?.Categories?.Count ?? 0;
    }
}
```

`AddScoped<CategoryService>()` creates your service per HTTP request. You can use
`AddTransient<CategoryService>()` instead when appropriate. `AddLunchMoney` already
registers the SDK client; do not register `LunchMoneyClient` separately. Avoid
keeping the transient SDK client in a singleton; inject the factory instead.

> **Authentication is included.** The token is sent as
> `Authorization: Bearer <token>` only to the configured host.

## Factory for singletons and jobs

`ILunchMoneyClientFactory` is a singleton that returns a configured
`LunchMoneyClient` for each `CreateClient()` call. See the complete
[background service example](background-services.md) for registration, injection
and recoverable failure handling.

## Manual construction

Registration, options and authentication configuration live outside `Generated`,
so regenerating the client does not overwrite them. You can also construct
`LunchMoneyClient` with an `HttpClient` and `IOptions<LunchMoneyOptions>`, or
continue using the generated `IRequestAdapter` constructor. When constructing
the client manually, the caller configures the HTTP transport, middleware and
option validation; use `AddLunchMoney` to get those defaults together.
