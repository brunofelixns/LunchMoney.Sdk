[Documentación](README.md) · [English](../en/architecture.md) · **Español**

# Arquitectura

El SDK separa la API generada, la configuración HTTP y los ciclos de vida de la
aplicación. Las aplicaciones utilizan directamente el `LunchMoneyClient` generado;
el código de integración proporciona su transporte, autenticación y opciones.

## Por qué se usa IHttpClientFactory

`AddLunchMoney` usa `AddHttpClient<LunchMoneyClient>` y devuelve
`IHttpClientBuilder`. Así, las aplicaciones pueden configurar el timeout, los
handlers delegados y el transporte mediante los mecanismos habituales de .NET.

`IHttpClientFactory` crea clientes HTTP de corta duración y mantiene un pool de
sus handlers subyacentes. Solicitar clientes nuevos permite rotar los handlers
y evita reconstruir la infraestructura de conexiones por operación. Consulta
la [guía de Microsoft sobre el ciclo de vida HTTP](https://learn.microsoft.com/dotnet/core/extensions/httpclient-factory#httpclient-lifetime-management).

El registro añade explícitamente el middleware predeterminado de Kiota al
transporte gestionado por la factory, incluidos los reintentos. La inyección
directa y la factory del SDK comparten la misma configuración HTTP con nombre;
las personalizaciones se aplican a ambas formas de uso. Esta capa también
concentra la validación de opciones y la autenticación Bearer limitada al host
configurado.

## Una factory para servicios en segundo plano

El cliente tipado `LunchMoneyClient` es transitorio y encaja en consumidores
scoped o transitorios. Un `BackgroundService` alojado es singleton; guardar un
cliente tipado en su constructor retendría su instancia HTTP durante toda la
vida del worker. Microsoft explica esta situación en
[Avoid typed clients in singleton services](https://learn.microsoft.com/dotnet/core/extensions/httpclient-factory#avoid-typed-clients-in-singleton-services).

`ILunchMoneyClientFactory` permite crear clientes del SDK desde estos consumidores
de larga duración. Su implementación singleton mantiene `IHttpClientFactory` e
`IOptions<LunchMoneyOptions>`, sin almacenar instancias de clientes HTTP o del SDK.
Cada `CreateClient()` solicita el cliente HTTP con nombre y construye un
`LunchMoneyClient` configurado mediante el mismo constructor partial que utiliza
la inyección tipada.

Los workers pueden solicitar un cliente por operación sin repetir las opciones,
la autenticación ni la construcción del adaptador. No necesitan un scope de DI
solo para obtener el SDK. Si la operación también utiliza servicios scoped,
como un `DbContext` de EF Core, sigue necesitando un scope apropiado para esos
servicios. Consulta el [ejemplo completo de servicio en segundo plano](background-services.md).

## Mantener el código personalizado fuera de Generated

`kiota update --clean-output` reemplaza el contenido de `Generated`. El código
de integración se mantiene fuera de esa carpeta para conservarlo al regenerar:

| Límite | Archivos | Responsabilidad |
| :--- | :--- | :--- |
| API generada | [Generated/](../../Generated/) | Request builders, modelos de la API y constructor del cliente generado. |
| Registro y opciones | [LunchMoneyServiceCollectionExtensions.cs](../../LunchMoneyServiceCollectionExtensions.cs), [LunchMoneyOptions.cs](../../LunchMoneyOptions.cs) | Registro de DI, pipeline HTTP y opciones de conexión validadas. |
| Creación del SDK | [ILunchMoneyClientFactory.cs](../../ILunchMoneyClientFactory.cs), [LunchMoneyClientFactory.cs](../../LunchMoneyClientFactory.cs) | Crear un cliente del SDK configurado por operación. |
| Integración del adaptador | [LunchMoneyClient.Extensions.cs](../../LunchMoneyClient.Extensions.cs) | Constructor partial que recibe el cliente HTTP y las opciones. |

El constructor partial crea el adaptador de Kiota y llama al constructor generado
que recibe `IRequestAdapter`. `ActivatorUtilitiesConstructor` selecciona el
constructor personalizado para la inyección tipada; el constructor generado
continúa disponible para integraciones manuales.

Al regenerar, conserva el nombre de la clase y su namespace, revisa las diferencias
de la API y ejecuta los tests. Consulta el comando exacto en
[Desarrollo](development.md) y el contrato de opciones en
[Configuración](configuration.md).
