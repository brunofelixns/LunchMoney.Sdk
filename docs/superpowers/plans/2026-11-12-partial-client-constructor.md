# Partial client constructor implementation plan

> **For agentic workers:** Use superpowers:executing-plans for inline implementation.

**Goal:** Construct the SDK using a custom partial constructor and standard
`AddHttpClient<LunchMoneyClient>()` registration backed by `IHttpClientFactory`.

**Architecture:** Put `LunchMoneyClient(HttpClient, IOptions<LunchMoneyOptions>)`
and adapter/authentication creation outside `Generated`. Keep `AddLunchMoney`
as the validated public registration API, preserving the named HTTP transport
and Kiota middleware.

**Tech Stack:** .NET 11, Microsoft.Extensions.Http/Options 11.0.0, Kiota 2.1.6, xUnit.

**Spec:** The constructor change approved in chat, extending
`../specs/2026-11-12-httpclientfactory-design.md`.

## Global constraints

- Preserve `Generated`, the target framework, and both `AddLunchMoney` overloads.
- Retain token/URL validation, configured-host authentication and Kiota handlers.
- Preserve consumer HTTP customizations, including `BaseAddress` overrides.
- Keep the HTTP client name `LunchMoney` and return `IHttpClientBuilder`.
- Use in-memory transports and synthetic tokens; do not call the real API.
- Work in place: the directory has no Git repository.

## Review focus

- DI resolves the new constructor using the factory-created HTTP client.
- Options supply the URL when the HTTP client has no `BaseAddress`.
- An overridden `BaseAddress` also controls the allowed authentication host.
- Existing startup validation, retries and transport settings stay effective.
- The original generated `IRequestAdapter` constructor remains available.

## Task 1: Constructor and typed registration

**Files:** Create `LunchMoneyClient.Extensions.cs`; update
`LunchMoneyServiceCollectionExtensions.cs`, `README.md`, and
`tests/LunchMoney.Sdk.Tests/RegistrationTests.cs`.

- [x] Add behavior tests for standard typed registration and overridden HTTP URLs.
- [x] Run the new tests and confirm the missing HTTP constructor causes failure.
- [x] Add the partial constructor with `ActivatorUtilitiesConstructor` and move
  adapter creation into it; use standard typed registration and retain handlers.
- [x] Update documentation with constructor usage and factory behavior.
- [x] Run the full test project and Release pack; compare generated-file hashes.
- [x] Obtain an independent review and address material findings.

## Execution notes

Ruling: Execute the already-approved design inline without another approval
round; no worktree/commit scripts apply because this directory has no Git data.

Ruling: Add only the requested options constructor. A raw-token overload and
transaction/model helpers are separate API additions outside this change.

RED: The standard typed registration test failed for both URL variants because
the client had no `HttpClient` constructor. The existing BaseAddress override
behavior passed. VSTest required execution outside the restricted sandbox.

GREEN: `dotnet test tests/LunchMoney.Sdk.Tests/LunchMoney.Sdk.Tests.csproj -c Release --no-restore`
passed all 24 cases. `dotnet pack LunchMoney.Sdk.csproj -c Release --no-restore`
created the Release package. Every file under `Generated` has the same SHA256
hash as before this change.

Final review: Independent read-only review found no actionable issues. Manual
construction continues to assign transport/middleware and validation to the
caller, as documented; generated endpoints and real API compatibility were
unchanged and outside this refactor. All review conditions (full suite, Release
pack and generated-file comparison) passed. Task 1: complete.
