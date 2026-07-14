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
- R9 immutable portal page revision migration in `db/postgresql/migrations/0012_portal_page_versions.sql`
- R14 durable content inventory and revisions in `db/postgresql/migrations/0016_content_items.sql`
- R14 durable publishing workflow, due schedules, and approval evidence in `db/postgresql/migrations/0017_publishing_workflow.sql`
- R7 PostgreSQL dashboard/analytics migration script in `db/postgresql/migrations/0007_dashboard_analytics.sql`
- Local PostgreSQL and Redis compose services with development connection string conventions
- Separate module projects for content, assets, workflow, data, search, SEO, agent access, dashboard, audit, identity, and jobs
- R10 reference pack portfolio for Finance, Technology, Education, and Entertainment under `industry-packs/`
- R10 industry pack manifest loader with fail-closed validation, resource checksums, and the `/IndustryPacks` admin catalog
- R10 audited, checksummed pack enablement state in `db/postgresql/migrations/0013_industry_pack_installations.sql`
- R11 shared HTTP production baseline with security headers, trace IDs, rate limiting, HSTS, and separate liveness/readiness endpoints
- R11 legacy-content migration analysis and controlled staging with traceable CSV validation, immutable batch journals, audited rollback, duplicate detection, missing-asset reporting, and URL mapping review
- R11 failure-safe search snapshot rebuilds that remove stale documents and keep the previous index on validation failure
- R11 bounded public `Cache-Control` policy for browser/CDN caching without caching admin, health, error, or cookie-setting responses
- R11 bounded public API pagination, conditional `ETag`/`Last-Modified` requests, and removal of content-list N+1 reads
- R11 PostgreSQL query audit with targeted analytics and public-page indexes in `0015_query_performance_indexes.sql`
- R11 closed acceptance baseline with fail-closed production hosts, upload signature validation, dependency scanning, and sequential CI tests
- R12 cross-platform `opk` CLI foundation with machine-readable boundary and AgentSEO readiness checks
- Crawlable HTML content at `/content/{slug}` and JSON-LD/Open Graph metadata on public content and block pages
- Architecture guardrail documents from R0
- Tested structural boundary checks for industry neutrality, module dependency direction, public credential-field leakage, and migration coverage

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

The normal test runner executes package-free unit and Host integration projects sequentially:

```powershell
./tools/run-tests.ps1
```

Set `OPK_POSTGRES_INTEGRATION` to include the isolated-schema PostgreSQL integration project. CI runs this against a
dedicated PostgreSQL 17 service even when a developer workstation has no local database configured.

## Developer CLI

Build the solution once, then run the repository checks through the cross-platform wrapper:

```bash
./tools/opk --help
./tools/opk new --name "Atlas Public Portal" --profile data --output ../atlas-public-portal
./tools/opk template pack --source . --output /tmp/openportalkit.opkt
./tools/opk module add --name Announcements --area publishing-support --description "Reusable announcement delivery contracts." --public-outputs JSON,Markdown
./tools/opk upgrade inspect --root ../atlas-public-portal --source .
./tools/opk check-boundaries
./tools/opk check-agent-readiness
./tools/opk check-agent-readiness --url https://portal.example.com
./tools/opk industry-pack add --name Example --output /tmp/opk-packs
./tools/opk industry-pack validate --path industry-packs
./tools/opk import legacy --input legacy.csv --assets assets.txt --output report.json --source legacy-mvc --import-batch batch-001 --as-of 2026-07-12 --schema-version legacy-content.v1
```

PowerShell users can invoke the same implementation through `./tools/opk.ps1`. Both checks support
`--format json` for CI and coding-agent integrations. A failed rule returns exit code `1`; invalid usage
returns exit code `2`.

Runnable portal examples and their versioned fixtures are documented in `examples/README.md`. Public contract
compatibility and release evidence requirements are defined in `docs/compatibility-policy.md` and
`docs/release-checklist.md`.

## Run Initial Hosts

```powershell
dotnet run --project src/OpenPortalKit.ApiHost
dotnet run --project src/OpenPortalKit.AdminHost
dotnet run --project src/OpenPortalKit.JobHost
```

The API host exposes:

- `/health`
- `/health/live`
- `/health/ready`
- `/api/system/modules`
- `/robots.txt`
- `/sitemap.xml`
- `/rss.xml`
- `/llms.txt`
- `/llms-full.txt`
- `/.well-known/agent.json`
- `/api/openapi.json`
- `/api/public/content`
- `/content/{slug}`
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
R10 Industry Pack System notes are in `docs/r10-industry-pack-system.md`.
R11 production hardening notes are in `docs/r11-production-hardening.md`.
R12 stabilization status and the 1.0 acceptance matrix are in `docs/r12-developer-experience.md`.
R13 source workspace scaffolding is documented in `docs/r13-project-scaffolding.md`.
R14 Admin Content Studio scope and ImageGen visual direction are documented in `docs/r14-admin-content-studio.md`.
Its server-backed content inventory uses `0016_content_items.sql`; review, scheduling, and approval evidence use
`0017_publishing_workflow.sql` when PostgreSQL persistence is enabled.

## Product Boundary

OpenPortalKit is a publishing framework, not a CRM, BI platform, trading system, low-code builder, or general data warehouse.

Core code must remain industry-neutral. Pack-specific content types, datasets, validation rules, dashboard cards, and templates belong only in their matching directory under `industry-packs/`.
