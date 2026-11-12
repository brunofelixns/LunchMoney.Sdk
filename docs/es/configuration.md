[Documentación](README.md) · [English](../en/configuration.md) · **Español**

# Configuración

| Opción | Obligatoria | Valor predeterminado |
| :--- | :---: | :--- |
| `LunchMoney:Token` | Sí | Tu token de acceso, sin el prefijo `Bearer`. |
| `LunchMoney:BaseUrl` | No | `https://api.lunchmoney.dev/v2` |

En el `appsettings.json` de la **aplicación que utiliza el SDK**:

```json
{
  "LunchMoney": {
    "BaseUrl": "https://api.lunchmoney.dev/v2"
  }
}
```

`BaseUrl` es opcional. Debe ser una URL HTTPS absoluta que incluya la versión de
la API, sin credenciales, parámetros de consulta ni fragmentos. Se admite una
barra al final.

## Desarrollo local

Define el token, sin el prefijo `Bearer`, en el **proyecto consumidor**. Para
desarrollo local, usa los secretos de usuario del proyecto ASP.NET Core o Worker:

```powershell
dotnet user-secrets init
dotnet user-secrets set "LunchMoney:Token" "YOUR_ACCESS_TOKEN"
```

Los ejemplos usan `WebApplication.CreateBuilder` o `Host.CreateApplicationBuilder`,
que cargan automáticamente los secretos del proyecto en el entorno `Development`.
Si el perfil de ejecución no selecciona ese entorno, inicia la aplicación con
`dotnet run --environment DOTNET_ENVIRONMENT=Development`.

## Despliegue

Proporciona la variable de entorno `LunchMoney__Token` mediante la configuración
de secretos de tu despliegue. Puedes cambiar la URL con `LunchMoney__BaseUrl`.
No guardes tokens reales en los archivos de configuración del repositorio.

## Configurar las opciones en código

También puedes configurar las opciones directamente:

```csharp
builder.Services.AddLunchMoney(options =>
{
    options.BaseUrl = "https://api.lunchmoney.dev/v2";
    options.Token = builder.Configuration["LunchMoney:Token"]!;
});
```

## Registro y validación

Las dos sobrecargas devuelven `IHttpClientBuilder`, de modo que puedes usar
`ConfigureHttpClient`, `AddHttpMessageHandler` y opciones de transporte adicionales.
Internamente, `AddLunchMoney` usa `AddHttpClient<LunchMoneyClient>`:
`IHttpClientFactory` crea el cliente HTTP y lo proporciona al constructor junto a
`IOptions<LunchMoneyOptions>`. El registro conserva el middleware predeterminado de
Kiota, incluidos los reintentos.

Las opciones inválidas provocan un error al iniciar el host o al resolver el
cliente desde un proveedor de servicios independiente. Las opciones se leen con
`IOptions<LunchMoneyOptions>`; reinicia la aplicación después de cambiar sus
datos de conexión.
