[Documentation](../README.md) · **English** · [Español](../es/configuration.md)

# Configuration

| Setting | Required | Default |
| :--- | :---: | :--- |
| `LunchMoney:Token` | Yes | Your access token, without the `Bearer` prefix. |
| `LunchMoney:BaseUrl` | No | `https://api.lunchmoney.dev/v2` |

In the **consuming application's** `appsettings.json`:

```json
{
  "LunchMoney": {
    "BaseUrl": "https://api.lunchmoney.dev/v2"
  }
}
```

`BaseUrl` is optional and defaults to the URL above. It must be an absolute HTTPS
URL, including the API version, without credentials, a query string or a fragment.
A trailing slash is accepted.

## Local development

Set the token in the **consuming project**, without the `Bearer` prefix. For local
development, use user secrets in the consuming ASP.NET Core or Worker project:

```powershell
dotnet user-secrets init
dotnet user-secrets set "LunchMoney:Token" "YOUR_ACCESS_TOKEN"
```

The examples use `WebApplication.CreateBuilder` or `Host.CreateApplicationBuilder`,
which load the project's user secrets automatically in the `Development`
environment. Use `dotnet run --environment DOTNET_ENVIRONMENT=Development` when
running locally if the launch profile does not already select that environment.

## Deployment

For deployments, supply the `LunchMoney__Token` environment variable through your
deployment's secret configuration. You can override the URL with
`LunchMoney__BaseUrl`. Do not commit real tokens to configuration files.

## Configure options in code

Alternatively, configure the options directly:

```csharp
builder.Services.AddLunchMoney(options =>
{
    options.BaseUrl = "https://api.lunchmoney.dev/v2";
    options.Token = builder.Configuration["LunchMoney:Token"]!;
});
```


## Registration and validation

Both overloads return `IHttpClientBuilder`, allowing further `ConfigureHttpClient`,
`AddHttpMessageHandler` and transport configuration. Internally, `AddLunchMoney`
uses `AddHttpClient<LunchMoneyClient>`: `IHttpClientFactory` creates the HTTP
client and supplies it to the custom constructor alongside
`IOptions<LunchMoneyOptions>`. Registration retains Kiota's default middleware,
including retries.
Invalid settings fail at host startup, or when the client is resolved from a
standalone service provider. Settings are read through `IOptions<LunchMoneyOptions>`;
restart the application after changing its connection settings.
