# OpenPortalKit

OpenPortalKit is an open-source framework for enterprise portals, content-heavy websites, and structured data publishing platforms.

The project is initialized as a modular monolith. The core is industry-neutral; vertical concepts belong in industry packs.

## Current State

- .NET 10 solution and host skeletons
- Kernel project with module, entity, event, audit, outbox, and job primitives
- Kernel outbox processing contracts with in-memory store, idempotency tracking, retry policy, and package-free tests
- Kernel audit recorder with in-memory query support by actor and target
- Generic content entities, slug generation, publish validation, version snapshots, audited publish events, and package-free content tests
- Read-safe public content query contracts that hide draft, archived, future, and expired content
- SEO baseline contracts for canonical URLs, Open Graph metadata, JSON-LD, sitemap XML, RSS, and robots.txt
- Legacy redirect rule resolution for public URL mapping foundations
- Publishing revalidation contracts for sitemap/RSS/snapshot regeneration and public route cache invalidation
- Publishing workflow transitions for draft, review, approval, rejection, scheduling, publishing, archive, restore, audit, and approval records
- Structured data dataset, schema version, import batch, CSV import/export, record traceability, checksum, dry run, quality report, snapshot, and public query contracts
- Search abstraction with in-memory provider, public/admin visibility rules, filters, repeatable reindexing, and outbox-driven indexing hook
- Dashboard and analytics contracts with privacy-conscious event capture, cached summaries, Prometheus text export, and .NET metrics publishing
- R7 PostgreSQL dashboard/analytics migration script in `db/postgresql/migrations/0007_dashboard_analytics.sql`
- Local PostgreSQL and Redis compose services with development connection string conventions
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
dotnet build OpenPortalKit.sln -m:1
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
- `/robots.txt`
- `/sitemap.xml`
- `/rss.xml`
- `/api/public/redirects/resolve`
- `/api/public/datasets`
- `/api/public/datasets/{code}`
- `/api/public/datasets/{code}/schema`
- `/api/public/datasets/{code}/records`
- `/api/public/datasets/{code}/records/{recordKey}`
- `/api/public/datasets/{code}/export.csv`
- `/api/public/search`
- `/api/public/search/health`

The admin host exposes:

- `/admin/dashboard/summary`
- `/admin/dashboard/snapshot`
- `/admin/dashboard/metrics.prometheus`
- `/admin/analytics/privacy`
- `/admin/analytics/events`
- `/analytics/events`

R7 dashboard and analytics notes are in `docs/r7-dashboard-analytics.md`.

## Product Boundary

OpenPortalKit is a publishing framework, not a CRM, BI platform, trading system, low-code builder, or general data warehouse.

Core code must remain industry-neutral. Finance-specific content types, datasets, validation rules, dashboard cards, and templates belong only in `industry-packs/Finance`.
