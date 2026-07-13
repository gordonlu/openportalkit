# Deployment

OpenPortalKit is being hardened for self-hosted production operation. Container and Windows Server packaging remain in progress; the HTTP production baseline below is implemented by both web hosts.

## Planned Local Services

- PostgreSQL
- Redis
- API host
- Admin host
- Job host
- Public web app

## Production Requirements

- Liveness at `/health/live` and readiness at `/health/ready`
- Structured log scopes and responses with validated `X-Trace-Id` values
- HTTPS redirection and HSTS outside Development
- Browser security headers and per-client fixed-window request limits
- OpenTelemetry export
- Secret management
- Database migration safety
- Backup and restore documentation
- Cache invalidation strategy
- Search index rebuild procedure

## Reverse Proxy

Terminate TLS at IIS, nginx, or another managed proxy and forward `X-Forwarded-For` and `X-Forwarded-Proto`. The application trusts the ASP.NET Core loopback proxy defaults. Explicitly configure known proxy addresses or networks when the proxy is not local; never trust forwarded headers from arbitrary clients.

Orchestrators should use `/health/live` only to decide whether the process must restart and `/health/ready` to decide whether it can receive traffic. `/health` remains a temporary redirect to readiness for compatibility.

## Production Configuration

`OpenPortalKit:Production` controls HTTPS redirection, HSTS, rate limiting, request budgets, and queue size. Defaults are defensive. Development configuration disables only HTTPS redirection so local HTTP launch profiles remain usable.

Readiness executes a validation query against enabled PostgreSQL persistence and a protocol-level `PING` against configured Redis. Disabled or unconfigured optional providers are not treated as failures. Search and object-storage probes will be added only when external providers replace their current in-process implementations.

Set `AllowedHosts` to the exact semicolon-delimited public hostnames for each web host; Production rejects an empty value or `*`. Set `OpenPortalKit__AgentAccess__OutputGeneration__PublicBaseUrl` to the external HTTPS origin for AdminHost and JobHost. Development permits localhost HTTP.

Provide connection strings, password hashes, OIDC client secrets, certificate passwords, and other credentials through environment variables or the Windows service secret facility. Do not add production secrets to `appsettings.json`. Run `tools/check-vulnerable-packages.ps1`, `npm audit --audit-level=moderate`, and the normal build before promoting a release artifact.

## Search Rebuild

The current in-memory search provider creates one complete index snapshot when ApiHost starts. Restart ApiHost to rebuild it from configured sources. Snapshot replacement validates every source first, removes stale documents, and keeps the previous snapshot when source loading or duplicate-ID validation fails.

Do not treat the in-memory provider as a shared multi-instance search service. A future PostgreSQL or external provider must implement atomic `ISearchIndex.ReplaceAllAsync` behavior and its own readiness probe before enabling rolling multi-instance rebuilds.

Publishing revalidation records contain route keys for cache eviction. Redis is currently checked for readiness but is not yet used as a page-cache provider, so deployments must not claim distributed cache invalidation until a concrete shared cache or CDN adapter is configured.

## Public Response Caching

ApiHost marks successful public GET and HEAD responses as browser/CDN cacheable. Configure bounded browser, shared-cache, and stale-while-revalidate durations under `OpenPortalKit:PublicCaching`; set `Enabled` to `false` when an upstream policy must own all cache headers. The policy does not apply to AdminHost, health endpoints, write requests, failures, responses that already define `Cache-Control`, or responses that set cookies.

The default shared-cache lifetime is five minutes and the browser lifetime is one minute. These bounded lifetimes limit stale exposure until a concrete CDN purge or shared-cache invalidation adapter consumes the route keys already emitted by publishing revalidation.

Content/page snapshots and structured-data resources also emit `ETag` and `Last-Modified`. Reverse proxies and CDNs should forward `If-None-Match` and `If-Modified-Since` and preserve upstream `304` responses. Do not strip resource validators when overriding cache lifetimes.

Public content, dataset-record, and search collections accept `offset` and `limit`; the maximum page size is 100. Clients should follow `hasMore` instead of assuming a fixed total count. Bulk dataset transfer should use the dedicated CSV export rather than requesting unbounded JSON pages.

## PostgreSQL Query Indexes

Migration `0015_query_performance_indexes.sql` supports global analytics retention and pagination, event-type filtering, and public-page listing. Apply it through the normal serialized migration tool before deploying the corresponding Store code. Index creation is transactional and may briefly lock writes on populated tables, so schedule the migration in a maintenance window when upgrading a high-volume analytics database.

After importing production-like data, use `EXPLAIN (ANALYZE, BUFFERS)` in a non-production clone to confirm planner behavior for the site's actual cardinality and filter distribution. Do not force index scans in application sessions; `enable_seqscan=off` is suitable only for verifying index eligibility during an audit.

## Database Migrations

Run migrations before starting a new application version:

```powershell
./tools/invoke-postgres-migrations.ps1
```

The script uses normal `psql` environment variables (`PGHOST`, `PGPORT`, `PGDATABASE`, `PGUSER`, and `PGPASSWORD`) so passwords are not passed as command arguments. It holds a PostgreSQL advisory lock for the complete run, executes each new file in its own transaction, records SHA-256 checksums in `opk_schema_migrations`, skips applied files, and fails when an applied migration was edited. Back up the database before applying a release and never rewrite an applied migration; add a new numbered file instead.

Migration `0014_legacy_migration_staging.sql` creates the legacy-import staging journal. Apply it before allowing operators to use `Validate and stage` in AdminHost. Staging is not content publication; production promotion remains unavailable.

## Backup and Restore

Create an atomic custom-format backup and checksum using normal `PG*` environment credentials:

```powershell
./tools/backup-postgres.ps1 -OutputDirectory ./backups
```

Validate the archive and checksum without changing a database:

```powershell
./tools/restore-postgres.ps1 -BackupPath ./backups/openportalkit-openportalkit-YYYYMMDDTHHMMSSZ.dump
```

Restore only during an approved maintenance window, after stopping all OpenPortalKit hosts and taking a separate backup of the target:

```powershell
./tools/restore-postgres.ps1 -BackupPath ./backups/openportalkit-openportalkit-YYYYMMDDTHHMMSSZ.dump -Database openportalkit -Apply
```

The restore uses one transaction with `--clean --if-exists`; an error rolls back the restore. Backup files contain production data and must be encrypted and access-controlled by the deployment environment.

## Administrator Credential

Generate the initial administrator password hash using hidden PowerShell input:

```powershell
./tools/new-admin-password-hash.ps1
```

Store the resulting value as the `OpenPortalKit__AdminAuthentication__PasswordHash` environment secret. Set the username separately with `OpenPortalKit__AdminAuthentication__UserName`. AdminHost production startup fails when authentication is required and the hash is absent. Rotate the credential by replacing the secret and restarting AdminHost; existing cookies remain bounded by their configured idle and absolute lifetimes.

For enterprise SSO, set `OpenPortalKit__AdminAuthentication__Mode=OpenIdConnect` and provide `Authority`, `ClientId`, `ClientSecret`, and `RequiredRole` through deployment configuration and secrets. The authority must use HTTPS. Prefer this mode for clustered production and keep the local credential as a controlled break-glass account only when operational policy requires it.

Changing `OpenPortalKit__AdminAuthentication__SessionVersion` and restarting AdminHost invalidates all existing cookies. For a web farm, configure `DataProtectionKeyPath` plus an X.509 certificate path/password and restrict the key directory ACL to the service account. Never mount an unencrypted shared key ring.
