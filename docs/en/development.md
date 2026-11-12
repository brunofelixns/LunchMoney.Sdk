[Documentation](../README.md) · **English** · [Español](../es/development.md)

# Development

## Regeneration

Run these commands from the repository root:

```powershell
dotnet tool restore
dotnet tool run kiota update -o ./Generated --clean-output
```

Kiota may delete and recreate everything inside `Generated`, including
`LunchMoneyClient.cs`. Keep custom code outside that directory:

```text
LunchMoneyOptions.cs                     # Custom settings
LunchMoneyServiceCollectionExtensions.cs # Custom registration
LunchMoneyClient.Extensions.cs           # Partial constructor and authentication
ILunchMoneyClientFactory.cs              # Factory contract for consumers
LunchMoneyClientFactory.cs               # Creates configured clients per operation
Generated/                              # Replaced by Kiota
```

`LunchMoneyClient.Extensions.cs` adds a constructor through a
`partial class LunchMoneyClient` in the `LunchMoney.Sdk` namespace. It creates
the adapter and calls the generated constructor without editing generated files.
The constructor is marked with `ActivatorUtilitiesConstructor` so typed client
registration selects it explicitly. Use the same class name and namespace when
regenerating, and run the tests afterward to detect API changes.
The local tool manifest pins Kiota to the version used by the generated client.
The specification URL in `Generated/kiota-lock.json` follows the upstream `latest`
specification, so regeneration can change the API surface. Review those changes
and update the package version when appropriate; CI builds the committed sources.

## Build and test

```powershell
dotnet restore LunchMoney.Sdk.slnx --locked-mode
dotnet build LunchMoney.Sdk.slnx -c Release --no-restore
dotnet test tests/LunchMoney.Sdk.Tests/LunchMoney.Sdk.Tests.csproj -c Release --no-build --no-restore
dotnet pack LunchMoney.Sdk.csproj -c Release --no-build --no-restore -o artifacts/packages
```

The integration tests use an in-memory HTTP transport and synthetic tokens. They
do not call Lunch Money. Error coverage exercises both injection styles, including
mapped/unmapped responses, malformed error JSON, retry recovery and exhaustion,
`Retry-After` seconds/dates, connection failure, timeout and cancellation before or
during HTTP and during retry delays.

`global.json` selects .NET SDK `11.0.100` with servicing patch updates allowed.
Commit both `packages.lock.json` files. After intentionally changing dependencies,
run `dotnet restore LunchMoney.Sdk.slnx --force-evaluate` and review the lock-file diff.

## CI and packages

[GitHub Actions](https://github.com/brunofelixns/LunchMoney.Sdk/actions/workflows/ci.yml)
runs locked restore, a Release build with warnings treated as errors, all tests
and packing on Windows and Linux for pushes to `main`, pull requests and manual
runs. Each successful job uploads a `.nupkg`, a `.snupkg` and a test report.
Artifacts expire after 14 days. No Lunch Money token is needed.

The NuGet package includes the README, MIT license, third-party notices and XML
API documentation. The symbol package contains portable PDBs; builds from a Git
checkout use the .NET SDK's built-in Source Link support for source debugging.

## Publishing a release

The CI validates and packages the SDK. To publish a release, choose the version in
`LunchMoney.Sdk.csproj`, merge and verify that commit's CI, then download one
successful job's package artifact. Publish the `.nupkg` using NuGet.org's upload
page or `dotnet nuget push` with your own publishing credentials. The companion
`.snupkg` enables symbol publishing. Package versions cannot be overwritten on
NuGet.org; retain the exact commit used to build each release.

The shared README uses absolute GitHub URLs and image hosts supported by
NuGet.org. Keep the linked documentation and banner available on `main`.
