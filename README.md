# OpenPortalKit

OpenPortalKit is an open-source framework for enterprise portals, content-heavy websites, and structured data publishing platforms.

The project is initialized as a modular monolith. The core is industry-neutral; vertical concepts belong in industry packs.

## Current State

- .NET 10 solution and host skeletons
- Kernel project with module, entity, event, audit, outbox, and job primitives
- Separate module projects for content, assets, workflow, data, search, SEO, agent access, dashboard, audit, identity, and jobs
- Finance Pack placeholder under `industry-packs/Finance`
- Architecture guardrail documents from R0
- Boundary check script for forbidden core terminology

## Repository Layout

```txt
apps/
src/
industry-packs/
templates/
docs/
examples/
docker/
tests/
tools/
```

## Build

```powershell
dotnet restore OpenPortalKit.sln
dotnet build OpenPortalKit.sln
powershell -ExecutionPolicy Bypass -File ./tools/check-boundaries.ps1
```

## Run Initial Hosts

```powershell
dotnet run --project src/OpenPortalKit.ApiHost
dotnet run --project src/OpenPortalKit.AdminHost
dotnet run --project src/OpenPortalKit.JobHost
```

The API host exposes:

- `/health`
- `/api/system/modules`

## Product Boundary

OpenPortalKit is a publishing framework, not a CRM, BI platform, trading system, low-code builder, or general data warehouse.

Core code must remain industry-neutral. Finance-specific content types, datasets, validation rules, dashboard cards, and templates belong only in `industry-packs/Finance`.
