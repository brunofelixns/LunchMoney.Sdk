[Documentación](README.md) · [English](../en/dependency-injection.md) · **Español**

# Inyección de dependencias

Elige la dependencia según el ciclo de vida del servicio que la utiliza. Las dos
formas comparten el [registro](getting-started.md) y la [configuración](configuration.md).

| Consumidor | Dependencia | Uso |
| :--- | :--- | :--- |
| Servicio de aplicación scoped o transitorio | `LunchMoneyClient` | Llama al SDK directamente. |
| Singleton, tarea programada o `BackgroundService` | `ILunchMoneyClientFactory` | Llama a `CreateClient()` en cada operación. |

## Inyección directa del cliente

Usa la inyección directa en servicios scoped o transitorios. Este `Program.cs`
completo de ASP.NET Core registra el SDK y un servicio, y expone un endpoint que
lo utiliza:

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

En `CategoryService.cs`, recibe el cliente configurado a través del constructor:

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

`AddScoped<CategoryService>()` crea el servicio por petición HTTP. Puedes usar
`AddTransient<CategoryService>()` si encaja mejor con tu aplicación.
`AddLunchMoney` ya registra el SDK; no registres `LunchMoneyClient` de nuevo.
Evita retener el cliente transitorio en un singleton: inyecta la factory.

> **La autenticación está incluida.** El token se envía como
> `Authorization: Bearer <token>` únicamente al host configurado.

## Factory para singletons y tareas

`ILunchMoneyClientFactory` es un singleton que crea un `LunchMoneyClient`
configurado en cada llamada a `CreateClient()`. Consulta el ejemplo completo de
[servicio en segundo plano](background-services.md), con registro, inyección y
gestión de fallos recuperables.

## Construcción manual

El registro, las opciones y la autenticación están fuera de `Generated`, por lo
que regenerar el cliente no los sobrescribe. Puedes construir `LunchMoneyClient`
con un `HttpClient` e `IOptions<LunchMoneyOptions>`, o seguir usando el constructor
generado que recibe `IRequestAdapter`.

Al construir el cliente manualmente, el código que lo crea debe configurar el
transporte HTTP, el middleware y la validación de opciones. Usa `AddLunchMoney`
para obtener esos valores predeterminados juntos.
