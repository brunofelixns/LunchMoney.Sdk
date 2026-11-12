# SDK client factory implementation plan

> **For agentic workers:** Use superpowers:executing-plans for inline implementation.

**Goal:** Let singleton/background consumers obtain a configured SDK client with
`ILunchMoneyClientFactory.CreateClient()`.

**Architecture:** Singleton SDK factory backed by the existing named
`IHttpClientFactory` client and options constructor; preserve typed injection.

**Tech Stack:** .NET 11, Microsoft.Extensions.Http/Options 11.0.0, Kiota 2.1.6, xUnit.

**Spec:** `../specs/2026-11-12-sdk-client-factory-design.md`.

## Global constraints

- Preserve `Generated`, .NET target, existing registration API and client constructor.
- Preserve validation, named transport, handlers and host-restricted Bearer token.
- Singleton factory must not retain individual HTTP/SDK clients or scoped services.
- Test with in-memory transports and synthetic tokens only.
- No Git repository exists here; work in place without Git/worktree operations.

## Review focus

- A singleton consumer can request fresh SDK/HTTP clients for each operation.
- Both consumption paths use the same HTTP configuration and Kiota pipeline.
- Configuration sections, URL overrides and authentication host restrictions work.
- Invalid settings fail through the factory as they do through typed injection.
- Document the factory lifetime and short-lived returned clients accurately.

## Task 1: Factory API, registration and examples

- [x] Declare the factory contract and add behavioral tests.
- [x] Verify new behavior fails because the factory has not been registered.
- [x] Implement the factory and register it in `AddLunchMoney`; share the HTTP name.
- [x] Document worker usage with `CreateClient()` and unchanged configuration.
- [x] Run the full test project, Release pack and generated-file hash comparison.
- [x] Obtain independent read-only review and resolve material findings.

## Execution notes

Ruling: The user's request specifies the factory's result and purpose; implement
that small addition inline while preserving the already-approved SDK design.

Ruling: Declaring the public interface alongside tests provides a compilable
contract for runtime RED verification; no factory behavior is implemented yet.

RED: All three new singleton/invalid-settings cases failed because
`ILunchMoneyClientFactory` was not registered. Tests compiled and ran through
real DI, with no external API requests.

GREEN: `dotnet test tests/LunchMoney.Sdk.Tests/LunchMoney.Sdk.Tests.csproj -c Release --no-restore`
passed all 31 cases. `dotnet pack LunchMoney.Sdk.csproj -c Release --no-restore`
created the Release package. Every generated-file SHA256 hash is unchanged.

Final review: No functional or material findings. Corrected the README's older
scope-per-operation advice to reference the new SDK factory, aligning both
examples. No runtime changes followed the passing suite. Task 1: complete.
