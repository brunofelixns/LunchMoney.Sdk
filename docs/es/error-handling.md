[Documentación](README.md) · [English](../en/error-handling.md) · **Español**

# Gestión de errores

El SDK propaga los fallos al `await` de quien lo utiliza. La inyección directa y
la factory tienen el mismo comportamiento; registrar el SDK no instala un
manejador global de excepciones en la aplicación.

| Fallo | Qué recibe la aplicación |
| :--- | :--- |
| Error HTTP con mapeo generado | `ErrorResponseObject`, derivado de `ApiException`, con estado, mensaje, cabeceras y `Errors`. Los mapeos dependen del endpoint. |
| Error HTTP sin mapeo | `ApiException`, con `ResponseStatusCode` y `ResponseHeaders`. |
| Reintentos de Kiota agotados | `AggregateException` con los fallos HTTP como entradas `ApiException`. |
| Problema de conexión | `HttpRequestException`. |
| Timeout HTTP o cancelación del consumidor | `OperationCanceledException`; comprueba el token del consumidor para distinguir el apagado de un timeout. |
| Opciones ausentes o inválidas | `OptionsValidationException` al iniciar el host o crear el cliente. |

## Reintentos

El handler predeterminado de Kiota reintenta `429`, `503` y `504`. En la versión
actual, al agotar los reintentos lanza una excepción agregada. Por eso un worker
con recuperación debe gestionar también ese caso, además de `ApiException`.
Los mensajes de las excepciones internas pueden contener cuerpos de respuesta;
el ejemplo registra el estado y el tiempo de espera, sin volcar esas excepciones.

## Dónde gestionar los fallos

En ASP.NET Core, gestiona los fallos en el servicio consumidor o en un manejador
de excepciones de la aplicación. En un `BackgroundService`, captura los fallos
recuperables dentro del bucle, como en
[Servicios en segundo plano](background-services.md).

Por defecto, una excepción no controlada de un servicio en segundo plano detiene
el host. Configurar `BackgroundServiceExceptionBehavior.Ignore` no reinicia un
bucle que haya terminado por una excepción.

Referencias: [errores en Kiota](https://learn.microsoft.com/openapi/kiota/errors),
[handler de reintentos](https://github.com/microsoft/kiota-dotnet/blob/main/src/http/httpClient/Middleware/RetryHandler.cs)
y [comportamiento de las excepciones en segundo plano](https://learn.microsoft.com/dotnet/core/compatibility/core-libraries/6.0/hosting-exception-handling).
