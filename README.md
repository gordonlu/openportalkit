# OpenPortalKit

OpenPortalKit is an open-source framework for enterprise portals, content-heavy websites, and structured data publishing platforms.

The project is initialized as a modular monolith. The core is industry-neutral; vertical concepts belong in industry packs.

## Current State

- .NET 10 solution and host skeletons
- Kernel project with module, entity, event, audit, outbox, and job primitives
- Kernel outbox processing contracts with leases, idempotency tracking, retry policy, PostgreSQL adapters, and package-free tests
- Kernel audit recorder with in-memory and PostgreSQL query support by actor and target
- Generic content entities, slug generation, publish validation, version snapshots, audited publish events, and package-free content tests
- Read-safe public content query contracts that hide draft, archived, future, and expired content
- SEO baseline contracts for canonical URLs, Open Graph metadata, JSON-LD, sitemap XML, RSS, and robots.txt
- Legacy redirect rule resolution for public URL mapping foundations
- Publishing revalidation contracts for sitemap/RSS/snapshot regeneration and public route cache invalidation
- Publishing workflow transitions for draft, review, approval, rejection, scheduling, publishing, archive, restore, audit, and approval records
- Structured data dataset, schema version, import batch, CSV import/export, record traceability, checksum, dry run, quality report, snapshot, and public query contracts
- Search abstraction with in-memory provider, public/admin visibility rules, filters, repeatable reindexing, and outbox-driven indexing hook
- Dashboard and analytics contracts with privacy-conscious event capture, AgentSEO readiness summaries, runtime health aggregation, dependency health probes, cached summaries, Prometheus text export, and .NET metrics publishing
- Agent Access / AgentSEO contracts for Markdown snapshots, JSON snapshots, llms.txt, llms-full.txt, agent manifest, public OpenAPI, and AI bot policy
- R9 Block Template System contracts for predefined, schema-versioned block instances, ordered page templates, configuration validation, and audited template revisions
- Server-rendered R9 public page baseline at `/pages/{slug}`, with canonical metadata and sitemap/RSS discovery
- R8 PostgreSQL agent output artifact migration script in `db/postgresql/migrations/0008_agent_output_artifacts.sql`
- R8 durable publishing delivery migration in `db/postgresql/migrations/0009_publishing_delivery.sql`, including JobHost processing, lease-based outbox claims, revalidation records, and audit history
- R9 versioned block template migration in `db/postgresql/migrations/0010_block_templates.sql`
- R9 portal page migration in `db/postgresql/migrations/0011_portal_pages.sql`
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
- `/llms.txt`
- `/llms-full.txt`
- `/.well-known/agent.json`
- `/api/openapi.json`
- `/api/public/content`
- `/api/public/content/{slug}.json`
- `/content/{slug}.md`
- `/api/public/redirects/resolve`
- `/api/public/datasets`
- `/api/public/datasets/{code}`
- `/api/public/datasets/{code}/schema`
- `/api/public/datasets/{code}/records`
- `/api/public/datasets/{code}/records/{recordKey}`
- `/api/public/datasets/{code}/export.csv`
- `/api/public/search`
- `/api/public/search/health`
- `/analytics/client.js`
- `/analytics/events`

The admin host exposes:

- `/admin/dashboard/summary`
- `/admin/dashboard/snapshot`
- `/admin/dashboard/metrics.prometheus`
- `/admin/analytics/privacy`
- `/admin/analytics/events`
- `/analytics/events`

R7 dashboard and analytics notes are in `docs/r7-dashboard-analytics.md`.
R8 Agent Access and AgentSEO notes are in `docs/r8-agent-access.md`.
R9 Block Template System notes are in `docs/r9-block-template-system.md`.

## Product Boundary

OpenPortalKit is a publishing framework, not a CRM, BI platform, trading system, low-code builder, or general data warehouse.

Core code must remain industry-neutral. Finance-specific content types, datasets, validation rules, dashboard cards, and templates belong only in `industry-packs/Finance`.
