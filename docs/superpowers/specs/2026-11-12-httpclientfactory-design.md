# Lunch Money SDK dependency injection

Implement the registration approved in chat: `AddLunchMoney` returns an
`IHttpClientBuilder`, creates the generated `LunchMoneyClient` with an
`IHttpClientFactory` client, and configures its URL and Bearer token outside
`Generated`.

## Approach

Use a typed client backed by the factory rather than requiring consumers to
construct Kiota adapters or introduce another client wrapper. Preserve the
generated API and its default Kiota handlers. Keep the existing `net11.0` target.

`LunchMoneyOptions` exposes `BaseUrl` (default
`https://api.lunchmoney.dev/v2`) and `Token` (required, without `Bearer`). Provide
overloads accepting an options action and an `IConfiguration` section. Validate
on host startup and on resolution. Base URLs must be absolute HTTPS URLs without
credentials, query strings or fragments, matching Kiota's authentication policy.
Only send the token to the configured host.

The returned HTTP builder supports timeout, handlers and transport customization.
Document configuration, user secrets, environment variables and constructor
injection. Verify with an in-memory HTTP transport: no real API calls or tokens.

The directory has no Git repository. Work in place and preserve existing files.
Compare hashes under `Generated`; account for external regeneration separately
from this implementation's edits.

## Verification outcome

The generated files were regenerated externally after the baseline was captured,
enabling backing stores and changing serializers. This implementation did not
edit them or invoke Kiota. Compilation and all 21 tests pass with the regenerated
files. The packed SDK was restored and exercised from a separate ASP.NET Core
consumer project with an in-memory transport.
