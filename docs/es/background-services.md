[Documentación](README.md) · [English](../en/background-services.md) · **Español**

# Servicios en segundo plano

`AddLunchMoney` registra `ILunchMoneyClientFactory` como singleton. Inyecta esta
factory en un singleton o `BackgroundService` y llama a `CreateClient()` en cada
operación. Cada llamada crea un cliente del SDK configurado mediante
`IHttpClientFactory`, con las mismas opciones, personalizaciones HTTP, handlers
de Kiota y autenticación limitada al host que la inyección directa. La factory
no retiene clientes HTTP ni clientes del SDK.

## Registro

Para una aplicación Worker independiente, usa este `Program.cs`. Consulta
[Configuración](configuration.md) para definir las opciones `LunchMoney` y el token:

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

## Worker con recuperación de fallos

En `LunchMoneyWorker.cs`, inyecta la factory y crea el cliente dentro de cada
iteración. Este ejemplo solo consulta datos: registra los fallos recuperables,
espera antes de volver a intentarlo y termina con normalidad cuando el host
cancela el token:

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

El ejemplo continúa después de respuestas HTTP `408`, `429` y `5xx`, reintentos
transitorios agotados, problemas de conexión y timeouts. Después de un fallo,
espera al menos 30 segundos y respeta un `Retry-After` mayor, tanto en segundos
como en formato de fecha HTTP. Tras una consulta correcta, espera cinco minutos.
Estos intervalos son decisiones de la aplicación de ejemplo, no valores
predeterminados del SDK.

Los errores permanentes, como `400` o `401`, las opciones inválidas y los fallos de
programación inesperados se propagan al host para que sean visibles. El bucle usa
`GET`; si lo adaptas a escrituras, define explícitamente su comportamiento de
reintentos e idempotencia.

## Integrarlo en ASP.NET Core

Para alojar este worker en una aplicación ASP.NET Core, añade
`builder.Services.AddHostedService<LunchMoneyWorker>()` antes de `builder.Build()`
en su `Program.cs`, con el mismo registro del SDK.

El worker no necesita recibir las opciones, conocer el nombre del cliente,
construir el adaptador ni crear un scope de DI solo para utilizar el SDK.
Mantén el cliente dentro de la operación. Si también usas servicios scoped,
como un `DbContext` de EF Core, resuélvelos dentro de un scope como de costumbre.

Consulta [Gestión de errores](error-handling.md) para entender las excepciones.
