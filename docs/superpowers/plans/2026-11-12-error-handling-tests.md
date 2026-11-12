# Error handling coverage and resilient worker

**Goal:** Test SDK failures through both direct injection and the SDK factory,
and document a worker that continues after recoverable failures.

**Scope:** Add characterization tests; preserve SDK runtime behavior and all
generated files. Use in-memory HTTP transports and synthetic credentials only.
Keep the README header, navigation and other usage examples.

## Decisions

- Cover mapped HTTP errors, unmapped statuses, malformed error JSON, network
  failures, bounded retries, Retry-After seconds/dates, timeout and cancellation
  before/during HTTP and during retry delays.
- Exercise actual Kiota middleware and HttpClient; do not substitute the SDK.
- Kiota can throw AggregateException containing ApiException entries when its
  retry budget is exhausted. Pin the installed version's behavior in tests and
  handle only aggregates of recoverable API failures in the example.
- The worker retries read-only polling after 408/429/5xx, exhausted transient
  retries, network failures and timeout. Shutdown cancellation exits normally.
  Permanent API errors and unexpected bugs remain visible to the host.
- Honor Retry-After on outer polling attempts as well as internal Kiota retries.
- No Git repository exists; work in place and compare generated-file hashes.

## Tasks

- [x] Add and run ErrorHandlingTests using real registered clients.
- [x] Record observed exception behavior; correct tests only against verified
  dependency contracts, without changing runtime behavior to satisfy a test.
- [x] Run a smoke check against the current README worker to demonstrate its
  failure on a recoverable response before changing the example.
- [x] Update the README worker and add a concise error handling guide.
- [x] Compile and exercise the exact documented worker in a temporary consumer;
  shorten only its fixed polling delays for verification.
- [x] Run the complete suite and Release pack; verify Generated is unchanged.
- [x] Obtain an independent review and resolve material findings.

## Verification notes

Ruling: SDK tests characterize existing behavior and may pass on first execution.
The new worker recovery behavior is verified RED before editing and GREEN after.

RED: The original README worker compiled, then the in-memory network-failure
smoke check failed because the host stopped rather than recovering.

Observed dependency contracts: exhausted retries throw AggregateException;
RetryHandler headers use a case-sensitive dictionary and normalize the request-ID
header's casing. Check HTTP header names case-insensitively. The synthetic network
transport must honor a pre-cancelled token before simulating I/O, like real I/O.

GREEN: All 71 SDK tests passed (40 new error cases plus the existing 31). The
worker recovered in network, timeout, 408/500/429/503/504 and Retry-After seconds/date
smoke scenarios, and shut down normally during HTTP, retry waits and normal polls.
Permanent-failure checks are verified separately against the host's failure path.
Release pack succeeded; every Generated-file SHA256 remained unchanged. README
Markdown and internal navigation targets render correctly.

Final verification: all 71 tests passed after removing the analyzer warning.
The extracted README worker compiled without warnings/errors and all 14 smoke
scenarios passed, including deliberate permanent/programming failures remaining
visible to the host. A read-only independent review found no actionable issues.
Only fixed polling delays were shortened in the temporary smoke consumer;
Retry-After timing and actual Kiota/HttpClient behavior were exercised unchanged.
All requested tasks are complete; SDK runtime and generated code are unchanged.
