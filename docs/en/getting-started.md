[Documentation](../README.md) · **English** · [Español](../es/getting-started.md)

# Getting started

The consuming application needs .NET 11. Once the package is published to
NuGet.org, install it with:

```powershell
dotnet add package LunchMoney.Sdk --version 0.1.0
```

## Install from CI artifacts

Version `0.1.0` is the initial package version prepared by this repository. Until
it is published to NuGet.org, download the package from a successful
[CI run](https://github.com/brunofelixns/LunchMoney.Sdk/actions/workflows/ci.yml)
and place `LunchMoney.Sdk.0.1.0.nupkg` in a `local-feed` directory in the consuming
project. Add a `NuGet.Config` beside that application's project file:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="LunchMoneyLocal" value="./local-feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

Then run from the consuming project:

```powershell
dotnet add package LunchMoney.Sdk --version 0.1.0 --no-restore
dotnet restore
```

The local feed supplies the SDK; NuGet.org supplies its transitive dependencies.
If your application already has a `NuGet.Config`, add the local source to its
existing sources instead of replacing that configuration.

## Register the SDK

Register the SDK in `Program.cs` before `builder.Build()`. Both injection styles
in [Dependency injection](dependency-injection.md) use this same registration;
call `AddLunchMoney` once per application:

```csharp
using LunchMoney.Sdk;
using Microsoft.Extensions.DependencyInjection;

builder.Services
    .AddLunchMoney(builder.Configuration.GetSection("LunchMoney"))
    .ConfigureHttpClient(http => http.Timeout = TimeSpan.FromSeconds(30));
```

`AddLunchMoney` registers `LunchMoneyClient` as a transient typed client and
`ILunchMoneyClientFactory` as a singleton. They share the same HTTP configuration,
authentication and Kiota middleware.

> **One registration, two ways to use it.** Inject `LunchMoneyClient` in regular
> services, or `ILunchMoneyClientFactory` when you need to create a client per operation.

Continue with [Configuration](configuration.md) to set the token.
