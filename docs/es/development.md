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

Los tests de integración usan un transporte HTTP en memoria y tokens ficticios;
no llaman a Lunch Money. Cubren ambas formas de inyección, respuestas con y sin
mapeo, JSON de error inválido, recuperación y agotamiento de reintentos,
`Retry-After` en segundos y fechas, errores de conexión, timeout y cancelación
antes y durante HTTP, y durante las esperas de reintento.

`global.json` selecciona .NET SDK `11.0.100` y permite actualizaciones de parche.
Incluye los dos archivos `packages.lock.json` en Git. Cuando cambies dependencias
de forma intencionada, ejecuta
`dotnet restore LunchMoney.Sdk.slnx --force-evaluate` y revisa las diferencias.

## CI y paquetes

[GitHub Actions](https://github.com/brunofelixns/LunchMoney.Sdk/actions/workflows/ci.yml)
restaura las dependencias bloqueadas, compila en Release tratando las advertencias
como errores, ejecuta los tests y genera paquetes en Windows y Linux. Se activa
en subidas a `main`, pull requests y ejecuciones manuales. Cada job correcto
guarda un `.nupkg`, un `.snupkg` y un informe de tests. Los artefactos caducan a los
14 días. No se necesita un token de Lunch Money.

El NuGet incluye el README, la licencia MIT, los avisos de terceros y la
documentación XML de la API. El paquete de símbolos contiene PDB portables;
las compilaciones desde Git usan Source Link integrado en el SDK de .NET para
depurar con el código fuente.

## Publicar una versión

La CI valida y genera los paquetes. Para publicarlos, elige la versión en
`LunchMoney.Sdk.csproj`, integra el commit y verifica su CI. Descarga el artefacto
de paquetes de uno de los jobs correctos. Publica el `.nupkg` desde la página de
subida de NuGet.org o con `dotnet nuget push` y tus propias credenciales. El
`.snupkg` permite publicar los símbolos. Las versiones no se pueden sobrescribir
en NuGet.org; conserva el commit exacto utilizado para cada versión.

El README compartido utiliza URL absolutas de GitHub y dominios de imágenes
admitidos por NuGet.org. Mantén la documentación enlazada y la cabecera
disponibles en `main`.
