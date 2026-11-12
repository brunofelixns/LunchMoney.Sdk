[Documentación](README.md) · [English](../en/getting-started.md) · **Español**

# Primeros pasos

La aplicación que utilice el SDK necesita .NET 11. Una vez publicado el paquete
en NuGet.org, instálalo con:

```powershell
dotnet add package LunchMoney.Sdk --version 0.1.0
```

## Instalar desde los artefactos de CI

La versión inicial preparada es `0.1.0`. Mientras no esté publicada en NuGet.org,
descarga el paquete de una [ejecución de CI correcta](https://github.com/brunofelixns/LunchMoney.Sdk/actions/workflows/ci.yml).
Extrae el artefacto y coloca `LunchMoney.Sdk.0.1.0.nupkg` en una carpeta
`local-feed` dentro del proyecto que utilizará el SDK. Crea un `NuGet.Config`
junto al archivo de proyecto de esa aplicación:

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

Después, ejecuta estos comandos desde el proyecto consumidor:

```powershell
dotnet add package LunchMoney.Sdk --version 0.1.0 --no-restore
dotnet restore
```

El origen local proporciona el SDK y NuGet.org proporciona sus dependencias.
Si la aplicación ya tiene un `NuGet.Config`, añade el origen local a su
configuración existente en lugar de sustituirla.

## Registrar el SDK

En `Program.cs`, registra el SDK antes de `builder.Build()`. Las dos formas de
[inyección de dependencias](dependency-injection.md) usan este mismo registro;
llama a `AddLunchMoney` una sola vez por aplicación:

```csharp
using LunchMoney.Sdk;
using Microsoft.Extensions.DependencyInjection;

builder.Services
    .AddLunchMoney(builder.Configuration.GetSection("LunchMoney"))
    .ConfigureHttpClient(http => http.Timeout = TimeSpan.FromSeconds(30));
```

`AddLunchMoney` registra `LunchMoneyClient` como cliente tipado transitorio y
`ILunchMoneyClientFactory` como singleton. Ambos comparten la configuración HTTP,
la autenticación y el middleware de Kiota.

> **Un registro, dos formas de usarlo.** Inyecta `LunchMoneyClient` en servicios
> normales o `ILunchMoneyClientFactory` para crear un cliente por operación.

Continúa con [Configuración](configuration.md) para definir el token.
