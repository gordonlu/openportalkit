# R11 Production Hardening and Migration Toolkit

R11 makes OpenPortalKit operable as a serious self-hosted service before adding legacy migration workflows. Work is delivered in reviewable batches and acceptance criteria are not marked complete until their real dependencies are exercised.

## Batch 1: HTTP Production Baseline

`OpenPortalKit.Infrastructure.Production` is a host-neutral ASP.NET Core infrastructure project shared by AdminHost and ApiHost. It owns:

- validated, bounded `X-Trace-Id` propagation and structured logging scopes
- Content Security Policy, clickjacking, MIME-sniffing, referrer, opener, and browser-permission headers
- forwarded-header handling with the framework's restricted proxy trust defaults
- configurable HTTPS redirection and production HSTS
- per-client fixed-window limits with bounded queues and Problem Details rejection bodies
- separate liveness and readiness endpoints
- startup validation for invalid request-limit configuration

Admin traffic receives a stricter default request budget than public API traffic. Health endpoints remain subject to the global limiter to prevent probe abuse, while their handlers are anonymous in preparation for the authentication batch.

## Verified Behavior

The package-free infrastructure tests cover security-header completeness, safe caller trace IDs, rejection of injection-shaped trace IDs, and defensive production defaults. A real ApiHost run verified both health endpoints, response headers, safe trace propagation, and random W3C replacement IDs.

## R11 Scope Status

R11 acceptance is complete for the providers that exist in the repository. Production configuration fails closed, public publishing is cacheable and conditionally readable, jobs are retry-safe, PostgreSQL changes are serialized and recoverable, and migration analysis/staging cannot modify public state. Provider-specific object storage, distributed response caching/CDN purge, external search, and migration promotion are not active providers and are not represented by no-op adapters.

## Batch 3: Dependency Readiness and Migration Safety

When PostgreSQL persistence is enabled, readiness opens the configured provider connection and executes `SELECT 1`. When a Redis connection is configured, readiness performs a bounded TCP connection and protocol-level `PING`. Liveness remains process-only, so a transient dependency failure removes the instance from service without forcing a restart.

`tools/invoke-postgres-migrations.ps1` serializes deployments with a PostgreSQL advisory lock, applies sorted SQL files in individual transactions, and records SHA-256 checksums. Previously applied files are skipped and checksum drift fails the deployment. The script uses `psql` environment credentials and removes its generated temporary control script after every outcome.

## Batch 4: Graceful Jobs and Recovery

JobHost stops claiming new outbox batches when shutdown begins and gives the current batch a configurable drain window. If the host timeout expires, cancellation returns control to the existing lease and idempotency safeguards. Shutdown state is logged without changing message payloads or retry semantics.

The PostgreSQL backup tool writes custom-format archives through a temporary file, atomically renames completed output, and emits a SHA-256 sidecar. Restore validates both checksum and archive structure by default; destructive restore requires `-Apply`, uses a single transaction, and exits on the first error.

## Batch 5: Migration Dry Run

The industry-neutral Migration module analyzes legacy-content CSV without writing source files or target data. Reports retain source, import batch, as-of date, schema version, source SHA-256, source row numbers, and timestamps. Validation covers required content fields, canonical and duplicate slugs, duplicate source IDs and old URLs, redirect loops, exact duplicate content, and attachment references against an operator-supplied inventory.

The authenticated `/Migration` page accepts only bounded CSV text uploads, validates strict UTF-8, rejects null characters, and holds the file in memory for the request. Apply remains unavailable until target writes and rollback can be atomic and audited.

## Batch 6: Controlled Migration Staging

Validated reports can now be staged without changing Content, Redirect, Asset, Search, sitemap, feed, or AgentSEO state. The PostgreSQL journal preserves the complete report plus source, import batch, as-of date, schema version, checksum, counts, operator, and timestamps. Source and import batch form an immutable identity: identical submissions are idempotent, while changed or rolled-back input requires a new batch.

Staging and rollback emit global audit events. Rollback is a state transition that retains the source report and actor trace instead of deleting evidence. Real PostgreSQL verification covered migration application, antiforgery-protected multipart staging, persisted report state, rollback, and both audit actions.

Persisted reports are downloadable as non-cacheable JSON from the protected migration page, including row-level errors and URL mappings for review or archival.

## Batch 7: Search Rebuild Safety

Search rebuild now assembles and validates the complete replacement snapshot before changing the active index. Duplicate document IDs or a failing source abort the operation and preserve the previous snapshot. A successful rebuild atomically replaces the snapshot, so documents removed from all sources no longer remain as stale search results.

ApiHost owns one search-index instance for its process lifetime and builds its current in-memory snapshot at startup. Public search and search health requests reuse that instance instead of rebuilding sample content for every request. Restarting ApiHost is therefore the current rebuild procedure for the in-memory provider. A future PostgreSQL or external-search provider must implement the same `ReplaceAllAsync` contract with transactional or alias-swap semantics before it is production-supported.

Route revalidation records identify the cache keys that need invalidation, but cross-process cache eviction is not claimed yet. That capability must be wired to the selected shared cache or CDN provider; Redis readiness alone is not a cache implementation.

## Batch 8: Public Cache-Control Baseline

ApiHost applies bounded, configurable browser and shared-cache directives only to successful public GET and HEAD responses. Existing cache headers, cookie-setting responses, errors, health checks, admin traffic, and writes are left untouched. This provides a CDN-compatible baseline without introducing an in-process response cache or claiming Redis-backed eviction before an actual adapter exists.

## Batch 9: Read API Efficiency

Public content, dataset-record, and search lists use a consistent bounded `offset`/`limit` contract with `hasMore`; limits above 100 and negative offsets fail with `400`. Search applies offset after stable score/date/title ordering. The public content query now materializes visible details directly from one store list call instead of performing a detail lookup per row, and ApiHost reuses its registered sample data context rather than rebuilding imports on every dataset request.

Content snapshots, rendered pages, dataset details, record pages, schemas, and CSV exports emit resource-derived `ETag` and `Last-Modified` validators. `If-None-Match` takes precedence over `If-Modified-Since`, and matching requests return bodyless `304` responses. Validators use domain timestamps and existing data checksums rather than buffering response bodies. AgentSEO OpenAPI documents pagination, validation failures, conditional responses, record lookup, and CSV export.

## Batch 10: PostgreSQL Query Audit

Production Store SQL was matched against every PostgreSQL index definition before adding indexes. Migration `0015_query_performance_indexes.sql` adds only verified gaps: global analytics time ordering/retention, event-type-only analytics filtering, and a partial public-page index keyed by site and display order.

Public page listing now calls an explicit Store method that applies site, `Published` status, and as-of publication time in PostgreSQL instead of loading every page for a site and filtering in application memory. In-memory behavior uses the same contract. Local PostgreSQL `EXPLAIN` confirmed the event-type query and public-page query can use index-only scans and retention deletion uses the global occurred-at index. Existing primary/unique indexes already cover idempotency, source/batch lookup, slugs, artifact paths, and version histories, so no duplicate indexes were added for those paths.

## Batch 11: Supply Chain, Upload Gate, and Production Fail-Closed

CI builds .NET with one MSBuild worker, executes every console test project, checks module boundaries, scans direct and transitive NuGet packages, audits npm dependencies at moderate severity, and performs lint plus a production Next.js build. The vulnerable PostCSS copy nested under Next is narrowly overridden to the patched release and locked; local audit reports zero vulnerabilities.

The Assets module now owns a reusable pre-storage upload validator. It rejects path-shaped/control-character filenames, empty or oversized files, unapproved extensions, declared MIME mismatches, signature mismatches, and SVG active content. No general upload route is exposed until a storage provider can retain verified metadata and opaque keys.

Production AdminHost and ApiHost reject wildcard `AllowedHosts`. AdminHost and JobHost reject a non-HTTPS Agent output base URL. Development remains explicitly exempt for localhost. Secrets remain external configuration values; committed JSON contains no production credential.

## Acceptance Evidence

| Criterion | Evidence |
| --- | --- |
| Admin routes are protected | Fallback authenticated Administrator policy, antiforgery, hardened cookie, local lockout, optional OIDC SSO |
| Public pages are cacheable | Bounded browser/CDN Cache-Control plus ETag and Last-Modified |
| Background jobs are retry-safe | Leased outbox, idempotency store, bounded retries, graceful drain |
| Failed imports do not corrupt data | Analysis is read-only; staging requires a valid report and writes an immutable isolated journal |
| Logs contain trace IDs | Validated X-Trace-Id middleware and logging scope |
| Health checks work in Docker | Process liveness plus real PostgreSQL SELECT 1 and Redis PING readiness |
| Legacy content supports dry run | Strict CSV analyzer with source checksum, row trace, duplicate/asset/route checks |
| Old URLs map to new URLs | Canonical redirect mappings and loop/duplicate detection in report |
| Migration errors export | Protected non-cacheable JSON report download |

R11 closes at controlled staging. Promotion is a separate feature because Content, Redirect, Asset, Search, audit, and rollback writes must first share a durable transaction boundary.

## Batch 2: Administrator Authentication

AdminHost now defaults to a fallback authorization policy that requires an authenticated `Administrator` role for every route. Login and health endpoints are explicit anonymous exceptions, static login assets remain available, and logout is POST-only with Razor antiforgery validation.

Credentials use a versioned salted PBKDF2-SHA512 format with 210,000 iterations and fixed-time comparison. Generate a deployment hash with `tools/new-admin-password-hash.ps1` and provide it through the `OpenPortalKit__AdminAuthentication__PasswordHash` secret or environment variable. Plaintext passwords do not belong in JSON configuration.

The authentication cookie is host-only, HTTP-only, `SameSite=Strict`, secure-only, non-persistent, and uses a sliding idle timeout bounded by an absolute session lifetime. Production startup fails closed when authentication is required but no supported password hash is configured. Development explicitly opts out in `appsettings.Development.json`; teams may override that setting to exercise authentication locally.

AdminHost uses a high-entropy per-request CSP nonce and does not permit arbitrary inline scripts. The remaining inline-style allowance is isolated from script execution and will be removed as component styling moves fully into static stylesheets. The non-Razor analytics POST performs explicit antiforgery validation in addition to authenticated-cookie and same-site controls.

### Brute-Force and Cookie Controls

The login endpoint has a dedicated per-source five-minute request budget in addition to the global AdminHost limit. Local authentication locks a source after the configured consecutive-failure threshold, returns the same response for unknown users, wrong passwords, and locked sources, clears failures only after success, and audits success, failure, and lockout without storing passwords. Local lockout state is process-local and is intended for a single-instance or emergency account; clustered production deployments should use OIDC and enforce distributed risk controls at the identity provider and edge gateway.

Every cookie carries an absolute start time and configurable session version. Changing `SessionVersion` revokes all existing local and OIDC sessions after restart. A custom shared Data Protection key ring is accepted only with an X.509 key-encryption certificate. On a single Windows Server, the framework's default user-profile key ring uses Windows protection; multi-instance deployments must configure a protected shared key repository. See Microsoft's [Data Protection configuration](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0) and [key storage guidance](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers?view=aspnetcore-10.0).

### Single Sign-On

Set `OpenPortalKit:AdminAuthentication:Mode` to `OpenIdConnect` to use an enterprise identity provider. AdminHost requires an HTTPS authority, client ID, client secret, Authorization Code flow with PKCE, and the configured `RequiredRole` claim. Tokens are not stored in the authentication cookie. Missing roles and remote failures are denied and audited. `Local` remains available as a separately controlled break-glass mode.
