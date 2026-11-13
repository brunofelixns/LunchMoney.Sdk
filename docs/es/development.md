[Documentación](README.md) · [English](../en/development.md) · **Español**

# Desarrollo

## Regeneración

Ejecuta los comandos desde la raíz del repositorio:

```powershell
dotnet tool restore
dotnet tool run kiota update -o ./Generated --clean-output
```

Kiota puede borrar y volver a crear todo el contenido de `Generated`, incluido
`LunchMoneyClient.cs`. Mantén el código personalizado fuera de esa carpeta:

```text
LunchMoneyOptions.cs                     # Custom settings
LunchMoneyServiceCollectionExtensions.cs # Custom registration
LunchMoneyClient.Extensions.cs           # Partial constructor and authentication
ILunchMoneyClientFactory.cs              # Factory contract for consumers
LunchMoneyClientFactory.cs               # Creates configured clients per operation
Generated/                              # Replaced by Kiota
```

`LunchMoneyClient.Extensions.cs` añade un constructor mediante una
`partial class LunchMoneyClient` en el namespace `LunchMoney.Sdk`. Crea el
adaptador y llama al constructor generado sin modificar los archivos generados.
El atributo `ActivatorUtilitiesConstructor` selecciona explícitamente ese
constructor en el registro del cliente tipado. Mantén el mismo nombre de clase y
namespace al regenerar, y ejecuta los tests para detectar cambios de API.

El manifiesto local fija la versión de Kiota a la utilizada para generar el
cliente. La URL de `Generated/kiota-lock.json` sigue la especificación `latest`
del proveedor, por lo que regenerar puede cambiar la API expuesta. Revisa esos
cambios y actualiza la versión del paquete cuando corresponda. La CI compila los
archivos que están en el repositorio.

## Compilación y tests

```powershell
dotnet restore LunchMoney.Sdk.slnx --locked-mode
dotnet build LunchMoney.Sdk.slnx -c Release --no-restore
dotnet test tests/LunchMoney.Sdk.Tests/LunchMoney.Sdk.Tests.csproj -c Release --no-build --no-restore
dotnet pack LunchMoney.Sdk.csproj -c Release --no-build --no-restore -o artifacts/packages
```

Los tests de integración de la solución predeterminada usan un transporte HTTP en memoria y tokens ficticios;
no llaman a Lunch Money. Cubren ambas formas de inyección, respuestas con y sin
mapeo, JSON de error inválido, recuperación y agotamiento de reintentos,
`Retry-After` en segundos y fechas, errores de conexión, timeout y cancelación
antes y durante HTTP, y durante las esperas de reintento.

`global.json` selecciona .NET SDK `11.0.100` y permite actualizaciones de parche.
Incluye el `packages.lock.json` de cada proyecto en Git. Cuando cambies dependencias
de forma intencionada, ejecuta
`dotnet restore LunchMoney.Sdk.slnx --force-evaluate` y revisa las diferencias.
Ejecuta también `dotnet restore tests/LunchMoney.Sdk.MockTests/LunchMoney.Sdk.MockTests.csproj --force-evaluate`
para actualizar el lockfile del proyecto opcional del mock, que no está en la solución.

## CI y paquetes

[GitHub Actions](https://github.com/brunofelixns/LunchMoney.Sdk/actions/workflows/ci.yml)
restaura las dependencias bloqueadas, compila en Release tratando las advertencias
como errores, ejecuta los tests y genera paquetes en Windows y Linux. Se activa
en subidas a `main`, pull requests y ejecuciones manuales. Cada job correcto
guarda un `.nupkg`, un `.snupkg` y un informe de tests. Los artefactos caducan a los
14 días. No se necesita un token de Lunch Money.

Un job separado, `official-mock`, ejecuta la suite de red en Linux y guarda el
artefacto `test-results-official-mock`. Debe pasar antes de publicar en NuGet.

El NuGet incluye el README, la licencia MIT, los avisos de terceros y la
documentación XML de la API. El paquete de símbolos contiene PDB portables;
las compilaciones desde Git usan Source Link integrado en el SDK de .NET para
depurar con el código fuente.

## Pruebas de integración con el mock oficial

El proyecto opcional `tests/LunchMoney.Sdk.MockTests` contiene un caso independiente
por cada una de las 65 operaciones HTTP del SDK, incluidas escrituras JSON,
borrados y subidas multipart de adjuntos. Una prueba de cobertura por reflexión
falla si la regeneración añade una operación sin caso o si hay casos duplicados.
Otras comprobaciones rechazan destinos ajenos al mock HTTPS oficial.

Ejecuta desde la raíz del repositorio:

```powershell
dotnet restore tests/LunchMoney.Sdk.MockTests/LunchMoney.Sdk.MockTests.csproj --locked-mode
dotnet test tests/LunchMoney.Sdk.MockTests/LunchMoney.Sdk.MockTests.csproj -c Release --no-restore --logger "trx;LogFileName=official-mock.trx" --results-directory artifacts/mock-tests
```

El proyecto está fuera de `LunchMoney.Sdk.slnx` para que la solución y el comando
de tests sin red no contacten con servicios externos. La CI lo ejecuta de forma
explícita en subidas, pull requests y ejecuciones manuales.

La suite fija el destino a `https://mock.lunchmoney.dev/v2` y genera un token
ficticio. No lee credenciales ni URL alternativas del entorno. Utiliza el registro
normal `AddLunchMoney`, la factory del SDK y el middleware de Kiota. Envía las
peticiones secuencialmente, separadas por al menos cuatro segundos, y limita
cada operación a 90 segundos. La ejecución completa tarda varios minutos.
Kiota gestiona los reintentos transitorios; los errores HTTP, timeouts, problemas
de red y fallos de aserción hacen fallar la suite, sin omitir casos silenciosamente.
Los errores HTTP esperados se declaran por caso y aparecen en el nombre del test;
deben coincidir tanto el estado como el tipo de excepción del código generado.

Cada caso comprueba el método HTTP real, la ruta y el estado esperado. Compara
los campos del JSON de entrada con el cuerpo enviado y verifica campos
representativos de la respuesta tras deserializarla a los modelos generados.
Los borrados sin contenido y las subidas multipart tienen sus propias aserciones.
Los datos proceden de los ejemplos oficiales de OpenAPI v2.11.2 y deben revisarse
al regenerar el cliente.

La [documentación oficial](https://lunchmoney.dev/introduction) describe un mock
estático. Las comprobaciones del 2026-11-13 observaron un comportamiento mixto:
las etiquetas persistían entre tokens ficticios, mientras que las altas de
categorías y cuentas manuales devolvían IDs que después no podían consultarse.
Los datos del mock público son compartidos; no es una cuenta desechable aislada.

La suite contiene actualmente 48 contratos de éxito y 17 de error explícito.
Las actualizaciones de recursos compartidos envían sus valores actuales; las
ediciones y borrados de etiquetas usan etiquetas propias con nombre único.
Las altas registran su limpieza en `finally`; antes de borrar, la limpieza
consulta el recurso y comprueba que pertenece al caso. Admite un 404 cuando el
mock no guardó el alta. Ningún test depende de las escrituras de otro test.

Para no eliminar datos de demostración compartidos, la mayoría de los DELETE
usan IDs inexistentes y esperan 404; el borrado de presupuestos espera 400.
Las escrituras de agrupación, división, subida de adjuntos y presupuestos también
usan IDs inválidos; el alta de criptomonedas comprueba el conflicto 422 de una
moneda existente. Esos casos validan peticiones y errores del SDK, no su CRUD
correcto. Actualmente `DELETE /transactions/group/{id}` devuelve un 404 HTML:
su caso comprueba explícitamente la propagación de `ApiException`. Es una
limitación del mock, no evidencia de que la desagrupación funcione.

La autenticación real, las reglas de negocio y la persistencia CRUD completa
necesitan un presupuesto de pruebas separado. Los errores, reintentos y
cancelaciones reproducibles permanecen en la suite sin red. Una caída del mock
solo hace fallar su job de CI y deja visibles los resultados de la suite sin red;
ambas suites forman parte de las comprobaciones de una versión.

Para comprobar solo el inventario, los destinos y la propiedad de recursos sin llamadas de red:

```powershell
dotnet test tests/LunchMoney.Sdk.MockTests/LunchMoney.Sdk.MockTests.csproj -c Release --filter "Category!=OfficialMock"
```

## Publicar una versión

La CI publica automáticamente en NuGet.org cuando los jobs de Windows, Linux y mock oficial
terminan correctamente en `main`. Descarga el paquete validado del job de Linux y
publica el `.nupkg` y su `.snupkg`, sin recompilar. Las pull requests y las
ejecuciones en otras ramas no pueden publicar. Los commits nuevos no cancelan las
ejecuciones de `main`, para permitir que una publicación en curso termine.

La autenticación utiliza [Trusted Publishing de NuGet](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
con OIDC de GitHub y una API key válida durante una hora. No se guarda una API key
permanente en GitHub. Configuración inicial:

1. Crea en GitHub un entorno llamado `nuget` y limita sus ramas de despliegue a
   `main`.
2. En NuGet.org, crea una política de Trusted Publishing con propietario del
   repositorio `brunofelixns`, repositorio `LunchMoney.Sdk`, archivo de workflow
   `ci.yml` y entorno `nuget`. Permite publicar paquetes nuevos y nuevas versiones,
   limitando el alcance al identificador `LunchMoney.Sdk`.
3. Define la variable de repositorio de GitHub Actions `NUGET_USER` con el nombre
   de usuario de la cuenta de NuGet.org (no su correo electrónico).

Para publicar una versión nueva, actualiza `Version` en `LunchMoney.Sdk.csproj` y
sube o integra el cambio en `main`. La versión inicial es `0.1.0`. Las versiones de
NuGet.org no se pueden sobrescribir; las subidas de una versión existente se
omiten. Puedes reintentar la publicación ejecutando manualmente el workflow `CI`
sobre `main`.

El README compartido utiliza URL absolutas de GitHub y dominios de imágenes
admitidos por NuGet.org. Mantén la documentación enlazada y la cabecera
disponibles en `main`.
