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
./tools/opk.ps1 check-agent-readiness
```

On Linux, use `./tools/opk check-boundaries` and `./tools/opk check-agent-readiness` after building.
Use `--format json` when another tool needs to consume the findings. Before a production release, run
`check-agent-readiness --url <public-base-url>` against the deployed candidate.

`tools/run-tests.ps1` starts the already-built ApiHost and AdminHost on random loopback ports and runs their
integration suite. PostgreSQL tests use an isolated random schema and require an explicit disposable test database:

```powershell
$env:OPK_POSTGRES_INTEGRATION = "Host=127.0.0.1;Port=15432;Database=openportalkit;Username=openportalkit;Password=openportalkit_dev"
./tools/run-tests.ps1
```

The PostgreSQL project is explicitly skipped when that variable is absent, but it remains mandatory in the dedicated
CI job. Never point it at a database account that cannot create and drop isolated schemas.

## Pull Request Expectations

- Explain the module touched and why.
- Note database migration impact.
- Note audit and permission impact.
- Note public API and AgentSEO impact.
- Note dashboard impact when metrics are added or changed.
- Keep industry-specific changes inside the relevant industry pack.
