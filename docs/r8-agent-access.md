# R8 Agent Access / AgentSEO

R8 adds first-class public outputs for search engines, LLMs, RAG systems, and browser agents.

## Public Outputs

- `GET /llms.txt`
- `GET /llms-full.txt`
- `GET /.well-known/agent.json`
- `GET /api/openapi.json`
- `GET /api/public/content`
- `GET /api/public/content/{slug}.json`
- `GET /content/{slug}.md`

The public API discovery document at `GET /api/public` includes links to content, datasets, search, sitemap, RSS, llms files, the agent manifest, and OpenAPI.

## Snapshot Semantics

Markdown snapshots include:

- title and summary
- published and updated timestamps
- canonical URL
- author and source
- key facts
- body text
- related links
- data sources
- usage policy
- agent visibility policy

JSON snapshots preserve the same public facts in machine-readable form, including citations, related content, usage policy, and visibility policy.

## Artifact Traceability

Agent-facing outputs can be materialized as `AgentOutputArtifact` records. Each artifact preserves:

- public path
- content type
- generated body
- source id and source kind
- schema version
- checksum
- generated timestamp

The in-memory artifact store is suitable for development and tests. Production wiring should replace it with persistent storage.

## AI Bot Policy

ApiHost reads `OpenPortalKit:AgentAccess:BotPolicy`:

```json
{
  "AllowSearchBots": true,
  "AllowTrainingBots": false,
  "AllowedUserAgents": [],
  "CrawlDelaySeconds": null
}
```

The default allows ordinary search crawling, blocks common training crawlers, and keeps explicit allow-list support for trusted agents. `robots.txt` emits the configured policy, including `Crawl-delay` when set.

## Agent Manifest

`/.well-known/agent.json` exposes:

- site name and description
- public resources
- sitemap and RSS
- llms.txt and llms-full.txt
- OpenAPI spec URL
- public search endpoint
- dataset endpoints
- bot policy
- usage and attribution policy

## Admin UI

The AdminHost Agent Access page is available at:

```txt
/AgentAccess
```

It shows Agent Readiness status, generated machine-readable resources, AI bot policy, readiness alerts, and snapshot coverage.

The page also exposes the public-output pipeline: publishing outbox, revalidation plan, artifact store, and audit log coverage.

## Revalidation and Audit

Publishing revalidation plans now include R8 agent-facing paths:

- `/content/{slug}.md`
- `/api/public/content/{slug}.json`
- `/llms.txt`
- `/llms-full.txt`

The recording executor supports an optional output regenerator. AgentAccess provides `AgentOutputArtifactRegenerator`, which writes generated artifacts and returns the persisted artifact paths to the revalidation result.

When an `AuditRecorder` is provided, public-output regeneration writes `public-output.revalidated` with invalidated routes and regenerated outputs in metadata. Repeated idempotency keys reuse the existing result and do not duplicate audit records.

## Boundaries

AgentAccess owns public machine-readable output contracts and policy models. It does not own content business state. Content remains in the Content module, datasets remain in the Data module, and dashboard summaries remain in Dashboard.

Current ApiHost development wiring registers a seeded `IContentItemStore`, then uses `PublicContentQueryService` for sitemap, RSS, llms files, snapshots, and public search indexing. Production wiring should replace the seeded store with persistent content storage and audit public-output-changing regeneration jobs.
