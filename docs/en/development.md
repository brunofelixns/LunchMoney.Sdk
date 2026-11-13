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

The default solution's integration tests use an in-memory HTTP transport and synthetic tokens. They
do not call Lunch Money. Error coverage exercises both injection styles, including
mapped/unmapped responses, malformed error JSON, retry recovery and exhaustion,
`Retry-After` seconds/dates, connection failure, timeout and cancellation before or
during HTTP and during retry delays.

`global.json` selects .NET SDK `11.0.100` with servicing patch updates allowed.
Commit each project's `packages.lock.json`. After intentionally changing dependencies,
run `dotnet restore LunchMoney.Sdk.slnx --force-evaluate` and review the lock-file diff.
Also run `dotnet restore tests/LunchMoney.Sdk.MockTests/LunchMoney.Sdk.MockTests.csproj --force-evaluate`
to update the opt-in mock project's lock file, which the solution does not include.

## CI and packages

[GitHub Actions](https://github.com/brunofelixns/LunchMoney.Sdk/actions/workflows/ci.yml)
runs locked restore, a Release build with warnings treated as errors, all tests
and packing on Windows and Linux for pushes to `main`, pull requests and manual
runs. Each successful job uploads a `.nupkg`, a `.snupkg` and a test report.
Artifacts expire after 14 days. No Lunch Money token is needed.

A separate `official-mock` job runs the network suite below on Linux. It uploads
`test-results-official-mock` and must pass before NuGet publishing can proceed.

The NuGet package includes the README, MIT license, third-party notices and XML
API documentation. The symbol package contains portable PDBs; builds from a Git
checkout use the .NET SDK's built-in Source Link support for source debugging.

## Official mock integration tests

The opt-in project `tests/LunchMoney.Sdk.MockTests` has one independent case for
each of the SDK's 65 HTTP operations, including JSON writes, deletes and multipart
attachment uploads. A reflection-based coverage test fails if regeneration adds
an operation without a case, or a case is duplicated. Additional checks reject
destinations outside the official HTTPS mock.

Run from the repository root:

```powershell
dotnet restore tests/LunchMoney.Sdk.MockTests/LunchMoney.Sdk.MockTests.csproj --locked-mode
dotnet test tests/LunchMoney.Sdk.MockTests/LunchMoney.Sdk.MockTests.csproj -c Release --no-restore --logger "trx;LogFileName=official-mock.trx" --results-directory artifacts/mock-tests
```

The project is deliberately outside `LunchMoney.Sdk.slnx`, so the default
solution and offline test command do not contact external services. CI invokes
it explicitly on pushes, pull requests and manual runs.

The suite fixes its destination to `https://mock.lunchmoney.dev/v2` and creates a
synthetic token. It does not read API credentials or endpoint overrides from the
environment. It uses the normal `AddLunchMoney` registration, SDK factory and
Kiota middleware, sends requests sequentially at least four seconds apart,
and bounds each operation to 90 seconds. Allow several minutes for a full run.
Kiota handles transient retries; HTTP errors, timeouts, network outages and
assertion failures fail the suite rather than silently skipping cases. Expected
HTTP errors are declared per case and shown in its test name; they must match
the expected status and generated exception type.

Each case checks the actual HTTP method, path and expected status. JSON request
fields are compared with the wire payload, and representative response fields
are checked after deserialization into generated SDK models. No-content operations
and multipart uploads have their own assertions. Fixtures come from the official
v2.11.2 OpenAPI examples and should be reviewed when regenerating the client.

The [official documentation](https://lunchmoney.dev/introduction) describes a static
mock. Probes on 2026-11-13 observed mixed behavior: tag writes persisted across
synthetic tokens, while category/manual-account creates returned IDs that could
not subsequently be read. Treat the public mock's data as shared, not as an
isolated disposable account.

The suite currently has 48 success contracts and 17 explicit error contracts.
Updates to shared fixtures send their existing values; tag updates/deletes use
uniquely named owned tags. Creation cases register cleanup in `finally`; cleanup
reads back the resource and checks ownership before deleting it, tolerating a
404 when the mock did not persist it. No test depends on another test's writes.

To avoid removing shared demo resources, most DELETE cases use absent IDs and
expect 404 (budget deletion expects 400). Group/split/upload writes and budget
upserts also use invalid IDs; cryptocurrency creation checks an existing coin's
422 conflict. These cover SDK requests and error deserialization, not successful
CRUD for those operations. The mock currently returns an HTML 404 for
`DELETE /transactions/group/{id}`; that case explicitly checks `ApiException`
propagation and is a mock limitation, not evidence that ungrouping works.

Real account authentication, business rules and full CRUD persistence require a
separate test budget. Deterministic error, retry and cancellation scenarios stay
in the offline suite. A public mock outage fails only its CI job, leaving the
offline job's results visible. Both suites are release checks.

To run only inventory, destination and ownership checks without network calls:

```powershell
dotnet test tests/LunchMoney.Sdk.MockTests/LunchMoney.Sdk.MockTests.csproj -c Release --filter "Category!=OfficialMock"
```

## Publishing a release

The CI automatically publishes to NuGet.org after the Windows, Linux and official mock jobs
succeed on `main`. It downloads the Linux job's validated package and publishes
the `.nupkg` and its companion `.snupkg`, without rebuilding. Pull requests and
runs on other branches cannot publish. Runs on `main` are not cancelled by newer
commits, so a publish already in progress can finish.

Authentication uses [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
with GitHub OIDC and an API key valid for one hour. No long-lived API key is stored
in GitHub. One-time setup:

1. Create a GitHub environment named `nuget` and restrict its deployment branches
   to `main`.
2. In NuGet.org, create a Trusted Publishing policy for repository owner
   `brunofelixns`, repository `LunchMoney.Sdk`, workflow file `ci.yml` and environment
   `nuget`. Allow publishing new packages and new versions, scoped to package ID
   `LunchMoney.Sdk`.
3. Set the GitHub Actions repository variable `NUGET_USER` to the NuGet.org account's
   username (not its email address).

To release a new version, update `Version` in `LunchMoney.Sdk.csproj` and push or
merge the change into `main`. The initial version is `0.1.0`. NuGet.org versions
cannot be overwritten; pushes with an existing version are skipped. You can
retry publishing by running the `CI` workflow manually on `main`.

The shared README uses absolute GitHub URLs and image hosts supported by
NuGet.org. Keep the linked documentation and banner available on `main`.
