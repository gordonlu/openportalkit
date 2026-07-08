# Tests

Planned test coverage:

- unit tests
- integration tests
- migration tests
- API tests
- admin workflow tests
- public rendering tests
- SEO public output snapshot tests
- search indexing tests
- background job tests
- dashboard aggregation tests
- AgentSEO snapshot tests
- industry pack tests
- security tests

Test projects should be added beside this file as features become executable.

## Current Test Projects

- `OpenPortalKit.Kernel.Tests`: package-free executable tests for early kernel behavior.
- `OpenPortalKit.Modules.Content.Tests`: package-free executable tests for generic content publishing behavior.
- `OpenPortalKit.Modules.Data.Tests`: package-free executable tests for structured data imports, CSV import/export, traceability, checksum change detection, dry runs, snapshots, and public dataset queries.
- `OpenPortalKit.Modules.Dashboard.Tests`: package-free executable tests for dashboard aggregation, alerts, privacy-conscious analytics events, session hashing, IP anonymization, bot classification, snapshot caching, Prometheus export, .NET metrics publishing, and PostgreSQL migration guardrails.
- `OpenPortalKit.Modules.Search.Tests`: package-free executable tests for search visibility, filters, idempotent reindexing, and outbox-driven indexing.
- `OpenPortalKit.Modules.Seo.Tests`: package-free executable tests for canonical metadata, sitemap, RSS, robots.txt, redirect resolution, and publishing revalidation behavior.
- `OpenPortalKit.Modules.Workflow.Tests`: package-free executable tests for review, approval, rejection, scheduling, archive, audit, and approval record behavior.
