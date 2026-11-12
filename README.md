<p align="center">
  <img src="https://raw.githubusercontent.com/brunofelixns/LunchMoney.Sdk/main/docs/assets/lunchmoney-sdk-header.png" alt="LunchMoney.Sdk — a .NET client for Lunch Money" width="100%" />
</p>

<p align="center">
  <strong>Lunch Money's API, ready for your .NET application.</strong><br />
  Typed requests, familiar dependency injection, and a factory for background work.
</p>

<p align="center">
  <a href="https://github.com/brunofelixns/LunchMoney.Sdk/actions/workflows/ci.yml"><img src="https://github.com/brunofelixns/LunchMoney.Sdk/actions/workflows/ci.yml/badge.svg?branch=main" alt="CI" /></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-11-512BD4?style=flat-square&amp;logo=dotnet&amp;logoColor=white" alt=".NET 11" /></a>
  <a href="https://lunchmoney.dev/v2/docs"><img src="https://img.shields.io/badge/Lunch_Money-API_v2-F6C744?style=flat-square&amp;labelColor=252238" alt="Lunch Money API v2" /></a>
  <a href="https://github.com/brunofelixns/LunchMoney.Sdk/blob/main/LICENSE"><img src="https://img.shields.io/badge/License-MIT-347568?style=flat-square&amp;labelColor=252238" alt="MIT license" /></a>
</p>

<p align="center">
  <strong>English</strong> · <a href="https://github.com/brunofelixns/LunchMoney.Sdk/blob/main/docs/es/README.md">Español</a> · <a href="https://github.com/brunofelixns/LunchMoney.Sdk/blob/main/docs/README.md">Documentation</a>
</p>

## Quickstart

Requires **.NET 11**. Get the initial `0.1.0` package from
[CI artifacts](https://github.com/brunofelixns/LunchMoney.Sdk/actions/workflows/ci.yml)
and follow the [local-feed installation guide](https://github.com/brunofelixns/LunchMoney.Sdk/blob/main/docs/en/getting-started.md).
After publication to NuGet.org, install with:

```powershell
dotnet add package LunchMoney.Sdk --version 0.1.0
```

Set `LunchMoney:Token` in your application's user secrets, or use the
`LunchMoney__Token` environment variable. Supply the token without `Bearer`.
The default API URL is `https://api.lunchmoney.dev/v2`.
See [configuration](https://github.com/brunofelixns/LunchMoney.Sdk/blob/main/docs/en/configuration.md)
for secret setup and validation.

In an ASP.NET Core application's `Program.cs`:

```csharp
using LunchMoney.Sdk;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddLunchMoney(builder.Configuration.GetSection("LunchMoney"))
    .ConfigureHttpClient(http => http.Timeout = TimeSpan.FromSeconds(30));

var app = builder.Build();

app.MapGet("/categories/count", async (
    LunchMoneyClient client, CancellationToken cancellationToken) =>
{
    var response = await client.Categories.GetAsync(cancellationToken: cancellationToken);
    return Results.Ok(response?.Categories?.Count ?? 0);
});

app.Run();
```

One registration supports both injection styles:

| Consumer | Inject |
| :--- | :--- |
| Scoped or transient service | `LunchMoneyClient` |
| Singleton or `BackgroundService` | `ILunchMoneyClientFactory`; call `CreateClient()` per operation |

Both share `IHttpClientFactory` configuration, host-restricted authentication and
Kiota middleware. Custom integration code lives outside `Generated`, so Kiota
regeneration preserves it.

## Documentation

| Guide | English | Español |
| :--- | :--- | :--- |
| Installation and local feeds | [Getting started](https://github.com/brunofelixns/LunchMoney.Sdk/blob/main/docs/en/getting-started.md) | [Primeros pasos](https://github.com/brunofelixns/LunchMoney.Sdk/blob/main/docs/es/getting-started.md) |
| Tokens, options and HTTP settings | [Configuration](https://github.com/brunofelixns/LunchMoney.Sdk/blob/main/docs/en/configuration.md) | [Configuración](https://github.com/brunofelixns/LunchMoney.Sdk/blob/main/docs/es/configuration.md) |
| Typed client and SDK factory | [Dependency injection](https://github.com/brunofelixns/LunchMoney.Sdk/blob/main/docs/en/dependency-injection.md) | [Inyección](https://github.com/brunofelixns/LunchMoney.Sdk/blob/main/docs/es/dependency-injection.md) |
| Resilient worker example | [Background services](https://github.com/brunofelixns/LunchMoney.Sdk/blob/main/docs/en/background-services.md) | [Segundo plano](https://github.com/brunofelixns/LunchMoney.Sdk/blob/main/docs/es/background-services.md) |
| Errors, retries and cancellation | [Error handling](https://github.com/brunofelixns/LunchMoney.Sdk/blob/main/docs/en/error-handling.md) | [Errores](https://github.com/brunofelixns/LunchMoney.Sdk/blob/main/docs/es/error-handling.md) |
| Kiota, tests, CI and releases | [Development](https://github.com/brunofelixns/LunchMoney.Sdk/blob/main/docs/en/development.md) | [Desarrollo](https://github.com/brunofelixns/LunchMoney.Sdk/blob/main/docs/es/development.md) |

## License and support

[MIT](https://github.com/brunofelixns/LunchMoney.Sdk/blob/main/LICENSE) ·
[Report an issue](https://github.com/brunofelixns/LunchMoney.Sdk/issues) ·
[Lunch Money API docs](https://lunchmoney.dev/v2/docs)

An independent community SDK, built with Microsoft Kiota. See
[third-party notices](https://github.com/brunofelixns/LunchMoney.Sdk/blob/main/THIRD-PARTY-NOTICES.md)
for brand assets and dependency licenses.
