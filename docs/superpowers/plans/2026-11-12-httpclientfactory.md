# LunchMoney HttpClientFactory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Register and configure the SDK through `AddLunchMoney` and `IHttpClientFactory`.

**Architecture:** Typed client registration with Kiota's middleware, validated
options, and host-restricted Bearer authentication. All custom code lives outside
`Generated`.

**Tech Stack:** .NET 11, Microsoft.Extensions.Http/Options 11.0.0, Kiota 2.1.6, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-12-httpclientfactory-design.md`

## Global Constraints

- Do not edit `Generated`; preserve external regeneration changes and keep `net11.0`.
- Support both configuration sections and options actions.
- Return `IHttpClientBuilder` for further HTTP customization.
- No Git operations: this directory is not a repository.
- Tests use synthetic tokens and in-memory HTTP handlers only.

## Review Focus

- Missing/invalid token or URL fails before sending HTTP requests.
- Custom base paths and trailing slashes produce the intended URLs.
- Tokens are not sent to unrelated hosts through Kiota's raw URL API.
- Factory configuration and Kiota retry middleware remain effective together.
- The packed SDK can be used from a separate consuming project.

## Task 1: Registration and configuration

**Files:** `LunchMoneyOptions.cs`, `LunchMoneyServiceCollectionExtensions.cs`,
`LunchMoney.Sdk.csproj`, `tests/LunchMoney.Sdk.Tests/*`, `README.md`.

**Interfaces:**
- `LunchMoneyOptions.BaseUrl`, `LunchMoneyOptions.Token`.
- `IHttpClientBuilder AddLunchMoney(this IServiceCollection, Action<LunchMoneyOptions>)`.
- `IHttpClientBuilder AddLunchMoney(this IServiceCollection, IConfiguration)`.

- [x] Add integration tests and run them; verify failures name the missing registration API.
- [x] Implement options, validation, factory registration and Kiota middleware.
- [x] Run `dotnet test tests/LunchMoney.Sdk.Tests/LunchMoney.Sdk.Tests.csproj -c Release`.
- [x] Document both overloads, secret configuration and consumer usage.
- [x] Build and pack Release; verify usage from a temporary separate project.
- [x] Review the completed change and compare `Generated` hashes with the baseline.

## Results

- Initial tests failed because `AddLunchMoney` was missing; implemented API passed
  all 20 cases after correcting the fake transport's response `RequestMessage`.
- Independent review found the factory's default primary transport bypassed
  Kiota's redirect middleware. Added a failing regression test, configured Kiota's
  default primary transport, and verified all 21 cases pass.
- Release build: zero warnings/errors. Package includes the DLL and README.
- Separate consumer restored the package and exercised configuration, DI, URL,
  Bearer authentication and response deserialization without network requests.
- VSTest's local socket is denied inside the sandbox; tests passed outside it.
- `Generated` hashes changed due to concurrent external Kiota regeneration,
  not this implementation. Preserved those files and verified the current SDK.
- Multi-account registration, hot reload and Native AOT are not added by this
  change; connection settings use the documented `IOptions` snapshot.
