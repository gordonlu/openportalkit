# Agent Instructions

Follow these rules when implementing OpenPortalKit.

## Non-Negotiable Boundaries

- Do not introduce industry-specific entities into core.
- Do not turn the project into a CRM, BI platform, trading system, full BPM system, low-code platform, or general data warehouse.
- Keep modules explicit and dependencies documented.
- Public-output-changing actions must be audited.
- Structured data must preserve source, import batch, as-of date, schema version, checksum, and timestamps.
- Public content should consider HTML, Markdown, JSON, sitemap, RSS, search, and AgentSEO readiness.
- Finance-specific concepts belong only in `industry-packs/Finance`.

## Definition of Done

A feature is not done until code, migrations, tests, docs, audit behavior, dashboard impact, AgentSEO behavior, and boundary impact have been considered.

## Implementation Order

1. Preserve module boundaries.
2. Add the smallest useful core abstraction.
3. Keep public APIs read-safe and documented.
4. Add tests at the risk boundary.
5. Run boundary checks before committing.

## Required Checks

```powershell
dotnet build OpenPortalKit.sln
powershell -ExecutionPolicy Bypass -File ./tools/check-boundaries.ps1
```
