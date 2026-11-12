# Short README and bilingual documentation

> **For agentic workers:** Use superpowers:executing-plans and verification-before-completion.

**Goal:** Keep English as the primary language, shorten the root README and move
the complete user documentation into matching English and Spanish guides.

**Architecture:** `README.md` is the concise package/repository landing page.
`docs/README.md` indexes English guides under `docs/en/`; `docs/es/README.md`
is the Spanish landing page and guide index. Each guide links to its translation.
Keep existing artwork, real CI badges and absolute root-README links for NuGet.

**Spec:** The user's request to shorten the README, move detail to docs and
provide the same documentation in Spanish and English, with English primary.

## Constraints

- Move all existing consumer guidance without changing the SDK or Generated.
- Preserve complete direct-injection and resilient BackgroundService examples.
- Use identical executable code in both languages; translate explanatory text.
- Retain installation from CI/local feeds until NuGet publication.
- Keep internal planning and artwork notes separate from user-facing indexes.
- Documentation edits need link/anchor, language parity, snippet compilation
  and package verification; no new runtime unit tests are required.

## Tasks

- [x] Create concise English README and Spanish landing page with language links.
- [x] Split installation, configuration, injection, background work, errors and
  development into six matching English/Spanish guide pairs.
- [x] Check links/anchors, render Markdown, compare code examples and compile
  the documented ASP.NET Core and Worker examples in temporary projects.
- [x] Pack with the shortened README and review the complete documentation change.

## Review focus

1. Missing original guidance after splitting the long README.
2. Broken relative links from nested Spanish/English pages or NuGet README links.
3. Divergent code samples or error/lifetime descriptions between languages.
4. Accidentally simplified cancellation, retry or worker failure behavior.
5. Claims of a public NuGet release when only CI packages exist.

## Verification record

README reduced from 497 to 92 lines. All 14 original code blocks survive in the
six English guides; each Spanish guide has identical code. All 86 repository
links resolve across 15 user-facing pages, and every page renders as Markdown.
The new quickstart, full ASP.NET Core service and complete Worker examples compile
from extracted code with zero warnings/errors. SDK source, Generated, tests and
workflow are unchanged.

Release pack succeeded. Archive inspection confirmed that its README is exactly
the shortened English source, with links to both documentation languages.
Independent read-only review found no actionable issues, including no changes to
the documented lifetime, exception, retry or cancellation contracts. Hosted
rendering and CI are verified separately after uploading the documentation.
