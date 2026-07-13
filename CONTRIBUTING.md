# Contributing

OpenPortalKit accepts changes that strengthen the publishing framework while preserving the core boundaries.

## Local Workflow

1. Read `AGENTS.md`, `ARCHITECTURE.md`, and `MODULES.md`.
2. Keep changes scoped to the relevant module.
3. Add or update tests for behavior changes.
4. Update docs when public contracts, module boundaries, AgentSEO outputs, dashboard metrics, or migration behavior change.
5. Run the single-worker build, all test projects, dependency scan, and boundary checks.

```powershell
dotnet build OpenPortalKit.sln -m:1
./tools/run-tests.ps1
./tools/check-vulnerable-packages.ps1
./tools/check-boundaries.ps1
```

## Pull Request Expectations

- Explain the module touched and why.
- Note database migration impact.
- Note audit and permission impact.
- Note public API and AgentSEO impact.
- Note dashboard impact when metrics are added or changed.
- Keep industry-specific changes inside the relevant industry pack.
