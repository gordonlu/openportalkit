# R12 Developer Experience and 1.0 Stabilization

## Scope

R12 turns the existing implementation into a stable product surface for maintainers, extension authors,
operators, and coding agents. It does not add unrelated portal domains. The release is complete only when
the public contracts, extension contracts, examples, tests, documentation, and deployment evidence satisfy
the 1.0 acceptance matrix below.

## Batch 1: CLI and Executable Guardrails

The `OpenPortalKit.Cli` project owns a cross-platform command contract with stable exit codes, text output for
humans, and JSON output for automation. Repository wrappers are available as `tools/opk` and `tools/opk.ps1`.
They use the already-built Debug output so checks do not trigger concurrent solution builds.

### Boundary Check

`opk check-boundaries` currently enforces:

- industry-specific terminology does not enter Kernel or generic modules;
- module project references follow the dependency directions documented in `MODULES.md`;
- source projects do not acquire dependencies on concrete industry packs;
- the public API does not reference known credential-oriented fields;
- every incremental PostgreSQL migration is explicitly referenced by a test.

The original `tools/check-boundaries.ps1` entry point remains compatible and delegates to this command.
Rule identifiers are stable within the 1.x line so CI systems can consume JSON findings.

### Agent Readiness Check

`opk check-agent-readiness` validates repository contracts for HTML, canonical metadata, JSON-LD, Markdown and
JSON snapshots, sitemap, RSS, agent discovery, OpenAPI, and dataset schema/records/export representations.

`opk check-agent-readiness --url <base-url>` probes a deployed candidate. It validates status codes, media
types, representative content HTML and snapshots, semantic metadata, discovery resources, and representative
dataset outputs. An empty content or dataset collection is reported as a warning because representation-level
validation is impossible without published examples.

The checker is read-only. It does not authenticate, mutate content, seed a site, or bypass bot policy.
Discovered content representations must remain on the requested origin, redirects are not followed, and response
bodies are bounded to prevent the checker from becoming an SSRF or unbounded-download path.

## Public Output Correction

The sitemap already advertised canonical `/content/{slug}` URLs, but ApiHost previously exposed only Markdown
and JSON forms. Batch 1 adds the missing semantic HTML representation and applies shared title, description,
canonical, Open Graph, and JSON-LD metadata to content and block pages. Conditional request behavior remains
in place. This is a public-output change and is covered by repository checks and live readiness validation;
it does not change stored publishing state or create an unaudited administrative action.

## Batch 2: Versioned Extension and Public Contracts

Industry pack manifests now declare `manifestVersion: 1.0`. The runtime loader, public JSON Schema, admin startup,
CLI author workflow, tests, and CI share that contract. Validation is strict about required and unknown properties,
resource containment, registration coverage, semantic versions, and minimum core compatibility. The command accepts
either one pack directory or a portfolio root.

The public API contract is versioned as `1.0.0` in OpenAPI and the
`X-OpenPortalKit-Contract-Version` response header. Every OpenAPI operation has a unique stable `operationId`, every
template variable has a required path parameter, search documents its required bounded query, and dataset record
pagination documents its actual default. The HTML content representation is now included in OpenAPI. Automated
tests enforce these invariants, while the live AgentSEO checker verifies contract headers on deployed API outputs.

## Batch 3: Host, Security, and PostgreSQL Integration

The Host integration project starts the already-built ApiHost and AdminHost assemblies on random loopback ports. It
does not invoke a nested build and shuts down each process even when an assertion fails. ApiHost coverage includes
OpenAPI/version headers, semantic public HTML, CSP and browser hardening headers, cache policy, ETag conditional reads,
pagination rejection, method safety, and trace propagation. AdminHost coverage includes anonymous challenge behavior,
antiforgery enforcement, generic credential failures, external return URL rejection, and the complete host-only secure
authentication cookie contract.

The PostgreSQL integration project creates a random schema, applies the durable publishing migration twice, exercises
outbox idempotency and exclusive leases, retry/processed transitions, audit JSON queries, and idempotency keys, then
drops the schema. It found and fixed a real duplicate-outbox defect where the insert reader remained open before the
same connection queried the existing idempotency row. CI runs this project in a separate PostgreSQL 17 service job.

## Batch 4: Authoring, Migration, and Runnable Examples

`opk industry-pack add` creates a non-overwriting manifest-v1 authoring workspace that immediately passes the shared
loader contract. `opk import legacy` runs the production migration analyzer offline, preserves source, import batch,
as-of date, schema version, checksum, analysis timestamp and row findings, writes atomically, and refuses to replace
existing evidence. Both commands have filesystem and unsafe-input boundary tests. Commands that would pretend to
seed or reindex a remote host were not added because no authenticated, audited remote command contract exists.

The five example portals are statically generated from one maintained Next.js application but use distinct product
layouts: corporate newsroom, dataset catalogue, research library, activity programme, and Finance Pack disclosure
centre. ImageGen-produced WebP assets show the actual published subject matter. Versioned `opk.example.v1` fixtures
declare each route, module set, outputs, and optional pack. Playwright runs with one worker across desktop and mobile,
checks image rendering, semantic headings, layout identity, console health, horizontal overflow, filtering, and
cross-template navigation. The generated UI has no runtime dependency on Playwright.

The 1.x compatibility policy classifies stable HTTP, CLI, manifest, migration, and documented module contracts; all
other public C# types remain implementation-visible until explicitly promoted with compatibility tests. The release
checklist separates repository evidence from Windows Server and real-provider OIDC rehearsals so environment work
cannot be marked complete from an Ubuntu development machine.

## 1.0 Acceptance Matrix

| Area | Current evidence | Status | Remaining R12 work |
| --- | --- | --- | --- |
| Core APIs | Public API v1, stable operation IDs, `docs/compatibility-policy.md` | Passing | Promote additional .NET types only with compatibility tests |
| Module boundaries | `MODULES.md`, CLI dependency/terminology checks, scaffolding fixtures | Passing | Keep the boundary gate required |
| Industry pack contract | Manifest v1 Schema, strict loader, CLI author validation, four reference packs | Passing | Preserve compatibility and add upgrade fixtures when v2 is designed |
| Content publishing | Versioned/audited services, public representations, Host HTTP regressions | Passing | Replace the current static workbench before claiming browser-based editing |
| Data publishing | Traceable imports, schema, records, snapshots, CSV and public representation tests | Passing | Add provider-specific scale tests with production data |
| Workflow and audit | Explicit transitions plus PostgreSQL outbox, lease, idempotency and audit round trips | Passing | Extend tests when new durable handlers are added |
| Dashboard | Operational, publishing, data, AgentSEO and provider-health fixtures | Passing | Revalidate thresholds with each production topology |
| AgentSEO | Static/live executable checks, Host semantic regressions, example browser semantics | Passing | Attach release-host evidence during RC rehearsal |
| Migration path | Controlled staging, offline CLI, guide and runnable legacy fixture | Passing | Promotion remains a separate transactional feature |
| Production deployment | Linux evidence, fail-closed configuration and RC checklist | External gate | Perform and attach Windows Server rehearsal evidence |
| Security baseline | Auth modes, lockout, scans, CSRF/redirect/cookie/header Host tests | External gate | Complete real OIDC provider and proxy threat rehearsal |
| Example sites | Five distinct SSG portals, manifests, ImageGen assets and Playwright suite | Passing | Keep fixtures aligned with public contracts |
| Critical tests | 16 sequential .NET projects, PostgreSQL CI job and 14 Web browser checks | Passing | Preserve single-worker build/test isolation |
| Core neutrality | CLI check and industry-pack boundary tests | Passing | Keep as a required release gate |

## R12 Closure

Repository implementation for R12 is complete. OpenPortalKit is a 1.0 release candidate only after the two external
gates in `docs/release-checklist.md` have evidence from the target Windows Server topology and the selected OIDC
provider. R12 does not claim remote seed/reindex commands, transactional legacy promotion, or a browser-based content
editor that the current backend contracts do not yet support.
