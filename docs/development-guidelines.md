# OpenPortalKit Development Guidelines

## Choose the Right Workflow

Use the repository directly when developing the framework, modules, hosts, CLI, migrations, or reference profiles.
Use `./tools/opk new` for a customer portal so branding, assets, and customer code have an independent Git history.
Use demo mode only for visual evaluation; use live mode and PostgreSQL for acceptance or deployment work.

Do not treat generated binaries as the customization surface. A customer workspace is source code that can be
reviewed, tested, built, and published from an approved commit.

## Host Responsibilities

| Host | Responsibility | Exposure |
|---|---|---|
| ApiHost | Public HTML, Markdown, JSON, datasets, search, feeds, OpenAPI, and AgentSEO | Public, behind HTTPS proxy |
| AdminHost | Authenticated authoring, review, publishing, templates, industry packs, and operations | Restricted administrator network/path |
| JobHost | Scheduled publication, outbox delivery, revalidation, and background work | No public HTTP exposure |
| Web | Customer-facing Next.js profile consuming ApiHost read contracts | Public, behind HTTPS proxy |

AdminHost and Web are separate products. Changing AdminHost CSS does not brand the public portal. Web must never
receive an administrator cookie, local password, OIDC client secret, draft payload, or internal database credential.

## Module Boundaries

- Keep Kernel focused on shared primitives and persistence contracts.
- Keep modules explicit; document new project references and preserve the existing dependency direction.
- Add the smallest generic abstraction that removes real duplication or establishes a required contract.
- Put all vertical entities, schemas, templates, and validation rules under the matching `industry-packs/<Industry>/`.
- Never move Finance, Technology, Education, Entertainment, or customer-specific vocabulary into core.
- Do not evolve OpenPortalKit into CRM, BI, trading, general BPM, low-code, or warehouse functionality.

Run `./tools/check-boundaries.ps1` after changing projects, migrations, public hosts, or industry packs.

## Content and Public Output

Public queries must filter drafts, archived items, future publication, expired items, private datasets, and internal
workflow evidence at the store or query-service boundary. Browser code is not a security boundary.

For every public content type, consider:

- semantic HTML for people and search engines;
- Markdown and JSON snapshots for agents and integrations;
- canonical URL and OpenGraph/JSON-LD metadata;
- sitemap and RSS discovery;
- search indexing and result links;
- `llms.txt`, agent manifest, and OpenAPI discoverability;
- cache invalidation and conditional requests;
- audit, outbox, and AgentSEO regeneration after publication.

Any action that changes public output must record the actor, target, summary, timestamp, and relevant metadata. It
must use existing workflow/outbox/revalidation paths rather than writing directly around them.

## Structured Data

Every imported record must preserve:

```text
source
generated import batch
as-of date
schema version
checksum
created and updated timestamps
```

Use `DataImportService` for validation and import. Do not write ad hoc records directly from UI handlers. Schema
versions are immutable; add a new version instead of modifying a schema already referenced by records. Public
catalogues expose only `IsPublic=true` datasets and must retain source/freshness fields in detail and export formats.

The structured-data module is a publishing facility, not a general warehouse or analytics engine.

## PostgreSQL Migrations

- Add the next numbered migration under `db/postgresql/migrations/`.
- Never edit an applied migration.
- Make migrations repeatable where possible with `if not exists`, while relying on the migration journal for order.
- Add a test reference for every migration so boundary check `OPK-BND-004` passes.
- Test new stores against an isolated PostgreSQL schema, not only by inspecting SQL text.
- Back up before production migration and retain the SHA-256 sidecar.
- Apply migrations only through `tools/invoke-postgres-migrations.ps1`; it takes an advisory lock and rejects drift.

Run builds and migrations sequentially. Do not start a second build against the same output folders while hosts or a
first build are still compiling.

## Security Rules

- Prefer OIDC Authorization Code with PKCE, provider MFA, and required-role enforcement for production AdminHost.
- Keep the local account as a controlled break-glass path with a strong generated hash and rate-limited lockout.
- Preserve host-only Secure HttpOnly SameSite cookies and antiforgery validation on state-changing requests.
- Configure exact `AllowedHosts`, trusted forwarded proxies, HTTPS, CSP, and Data Protection key storage.
- Validate upload signatures, extension/size limits, canonical paths, redirects, and external URLs.
- Never commit `.env`, connection strings, credentials, production password hashes, certificates, key rings, backups,
  logs, database volumes, `node_modules`, or build caches.
- Public Web server clients may call public GET contracts only. Rebuild returned links against an approved public
  origin and reject paths outside documented public families.

Security failures must be generic to users and detailed only in protected logs. Authentication responses must not
reveal whether a user name exists.

## Web and UI Work

- Preserve the five profile structures; do not reduce them to one layout with color swaps.
- Use ImageGen assets for intentional visual direction, then keep repository assets optimized and attributable.
- Keep public pages semantic, responsive, keyboard accessible, and readable without client JavaScript where feasible.
- Keep server data fetching in server-only modules. Limit client components to interaction such as filtering/search.
- Define explicit loading, empty, dependency-unavailable, and validation-error states.
- Do not silently replace failed live data with fixtures.
- Avoid arbitrary runtime CSS/script injection. Source-level React/CSS customization remains supported.
- Keep public identity and approved asset references in `apps/web/src/lib/branding.json`; run
  `./tools/opk branding validate --root .` after changing them.
- Verify desktop and mobile with Playwright after layout or interaction changes.

## Testing by Risk

Run focused tests while editing, then the full acceptance gates before commit.

| Change | Minimum focused coverage |
|---|---|
| Domain validation | Module unit tests |
| Public API shape | OpenAPI/contract and Host integration tests |
| Store or migration | Unit plus PostgreSQL isolated-schema integration |
| Authentication/security | Identity and Host security integration |
| Publishing behavior | Workflow, audit, outbox, and AgentSEO tests |
| Web UI | lint, production build, desktop/mobile E2E |
| Branding or public assets | branding validator, Web build, desktop/mobile E2E |
| Live Web client | success and dependency-failure live E2E |
| Project/module boundary | boundary check and solution build |

Required repository gates:

```bash
dotnet build OpenPortalKit.sln -m:1
pwsh -NoProfile -File ./tools/check-boundaries.ps1
pwsh -NoProfile -File ./tools/run-tests.ps1
```

Required Web gates from `apps/web`:

```bash
npm run lint
npm run build
PLAYWRIGHT_CHROME_PATH=/usr/bin/google-chrome npm run test:e2e
PLAYWRIGHT_CHROME_PATH=/usr/bin/google-chrome npm run test:e2e:live
```

## Change Completion

A feature is complete only after considering code, migration, tests, documentation, audit behavior, dashboard impact,
AgentSEO behavior, compatibility, and module boundaries. Not every change requires work in every area, but the review
must be explicit.

Before committing:

1. Review `git diff` for secrets, generated output, unrelated edits, and accidental fixture/live mixing.
2. Run `git diff --check`.
3. Run focused tests and required gates sequentially.
4. Update user-facing configuration and operations documentation.
5. Record intentionally deferred work in `roadmap.md`; do not hide it behind a placeholder implementation.

Deployment and Windows operations are covered by `deployment.md`; release evidence is covered by
`release-checklist.md`.
