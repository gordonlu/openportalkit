# Modules

Modules are code ownership boundaries inside the modular monolith.

## Kernel

`OpenPortalKit.Kernel` owns shared primitives:

- module descriptors
- base entity interfaces
- domain events and integration events
- audit log records
- outbox messages
- job records
- site, user, role, permission, setting, and asset primitives

Kernel must stay industry-neutral.

## Module List

- Identity: users, roles, permissions, authentication, authorization
- Content: content types, content items, versions, taxonomies
- Assets: uploads, media metadata, file validation
- Workflow: publishing workflow, approvals, state transitions
- Data: datasets, schema versions, records, imports, snapshots, views
- Search: search documents, providers, indexing jobs
- SEO: metadata, sitemap, RSS, redirects, structured data
- AgentAccess: Markdown snapshots, JSON snapshots, llms.txt, AI bot policy, public OpenAPI
- Dashboard: operational and publishing health metrics
- Audit: audit queries and retention behavior
- Jobs: job scheduling, retries, idempotency, background handlers
- Migration: traceable dry-run analysis and controlled legacy import workflows

## Dependency Rules

- Modules may depend on Kernel.
- Host projects may depend on modules.
- Modules should not depend on host projects.
- Cross-module calls should go through explicit contracts or events.
- Dashboard reads and aggregates; it must not own source business state.
- Industry packs may use generic module extension points but must not create core dependencies.
- Migration may depend on generic Content, Data, and SEO contracts; those modules must not depend on Migration.
