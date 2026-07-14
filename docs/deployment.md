# Customer Customization and Deployment

## Delivery Model

OpenPortalKit is a source framework. A customer should generate an independent workspace from a tagged source
release or verified `.opkt` archive, then keep customer-specific code and assets in that workspace:

```bash
./tools/opk new \
  --name "Atlas Public Portal" \
  --profile corporate \
  --output ../atlas-public-portal
```

Maintaining a GitHub fork is also valid when the customer intends to contribute framework changes upstream. For a
normal private portal, a generated workspace gives clearer provenance and avoids coupling customer releases to the
framework repository's branch history.

## What Can Be Customized Today

| Area | Source location | Contract |
|---|---|---|
| Public images and static files | `apps/web/public/` | Customer-owned files; do not commit secrets or private source documents. |
| Public visual system | `apps/web/src/app/globals.css` | Source-level CSS is supported and remains subject to responsive and accessibility tests. |
| Public composition | `apps/web/src/components/` | React/Next.js source; preserve semantic HTML and public-output discoverability. |
| Public identity | `apps/web/src/lib/branding.json` | Versioned site identity, approved assets, contrast-checked colors, navigation, and footer. |
| Project provenance | `apps/web/src/lib/project-profile.json` | Generated profile metadata and selected industry packs. |
| Admin appearance | `src/OpenPortalKit.AdminHost/wwwroot/css/site.css` | Operational UI only; this does not style the public site. |
| Portal page blocks | `OpenPortalKit.Modules.Content/BlockTemplates` | Schema-versioned definitions, validation, rendering, migration consideration, and tests are required. |
| Industry concepts | `industry-packs/<Industry>/` | Never add industry entities to core. |

The Next.js application defaults to explicit demo fixtures. Its R15 live mode server-renders published content,
portal pages, and the public dataset catalogue from ApiHost with response validation, bounded fetches, and no fixture
fallback. Interactive search uses a same-origin read-only proxy, while canonical/OpenGraph metadata uses the approved
public origin; see `r15-public-web-runtime.md`.

Brand assets are validated separately from content publication. Place customer assets under `apps/web/public/`, edit
the versioned branding manifest, and run `./tools/opk branding validate --root .`. The validator rejects unsafe SVG,
path traversal, unsupported or oversized files, false dimensions, unsafe links, and insufficient contrast. A null
logo intentionally falls back to the configured short name. See `r15-branding-assets.md`.

## Repository Acceptance Before Packaging

Run builds sequentially on this repository because concurrent host builds can contend for output files:

```powershell
dotnet restore OpenPortalKit.sln
dotnet build OpenPortalKit.sln -m:1
./tools/run-tests.ps1
./tools/opk check-boundaries
./tools/opk branding validate --root .
./tools/check-vulnerable-packages.ps1
```

In `apps/web` run:

```bash
npm ci
npm run lint
npm run build
npm run test:e2e
npm run test:e2e:live
```

Configure `OPK_POSTGRES_INTEGRATION` when running repository acceptance so the PostgreSQL test project is not
skipped. Never package `.env`, development databases, backups, Data Protection keys, certificates, logs,
`node_modules`, or build caches.

## Current Windows Publish Procedure

The following creates framework-dependent Windows x64 host directories. Install the matching .NET 10 ASP.NET Core
Runtime on the server. Use `--self-contained true` only when the deployment policy requires carrying the runtime.

```powershell
dotnet publish src/OpenPortalKit.ApiHost/OpenPortalKit.ApiHost.csproj `
  -c Release -r win-x64 --self-contained false -m:1 -o artifacts/win-x64/api
dotnet publish src/OpenPortalKit.AdminHost/OpenPortalKit.AdminHost.csproj `
  -c Release -r win-x64 --self-contained false -m:1 -o artifacts/win-x64/admin
dotnet publish src/OpenPortalKit.JobHost/OpenPortalKit.JobHost.csproj `
  -c Release -r win-x64 --self-contained false -m:1 -o artifacts/win-x64/jobs
```

These commands are a manual packaging procedure, not yet the final R15 verified artifact pipeline. They do not
register Windows Services, configure IIS, install PostgreSQL tools, provision certificates, or produce a release
manifest/checksum bundle. The Next.js application is built separately with `npm ci` and `npm run build` and currently
requires Node.js plus `npm run start`.

## Database Promotion

PostgreSQL is the production persistence baseline. Before every migration:

1. Run `tools/backup-postgres.ps1` and retain its SHA-256 sidecar.
2. Verify the backup can be inspected or restored in an isolated database.
3. Set standard `PGHOST`, `PGPORT`, `PGDATABASE`, `PGUSER`, and `PGPASSWORD` environment variables through the
   deployment secret store.
4. Run `tools/invoke-postgres-migrations.ps1`. It takes an advisory lock, records checksums, and rejects drift.
5. Never edit an applied numbered migration; add the next migration.

`0016_content_items.sql` and `0017_publishing_workflow.sql` are required for the R14 Content Studio workflow.
`0018_structured_data.sql` adds durable, traceable datasets, schema versions, and records for the R15 public runtime.

## Required Production Topology

- ApiHost runs continuously behind an HTTPS reverse proxy and serves public read contracts.
- AdminHost runs behind authentication and must not be exposed without HTTPS and exact host configuration.
- JobHost runs continuously. Outbox processing, AgentSEO regeneration, and scheduled publication depend on it.
- PostgreSQL must be backed up, monitored, and reachable by all three hosts that use durable persistence.
- The public Web application, when used, is a separate Node.js process until R15 defines a verified artifact model.

For the public Web process, configure:

```powershell
$env:OPK_WEB_DATA_MODE = "live"
$env:OPK_API_BASE_URL = "http://127.0.0.1:5051"
$env:OPK_PUBLIC_BASE_URL = "https://portal.example.com"
```

`OPK_API_BASE_URL` is server-only and may point to an internal listener. `OPK_PUBLIC_BASE_URL` is the externally
reachable HTTPS origin used in browser links. Do not include credentials in either URL. A live-mode dependency
failure renders an explicit unavailable state and never substitutes demo records.

Set production values outside committed JSON. At minimum configure exact `AllowedHosts`, the PostgreSQL connection,
both PostgreSQL persistence switches, the public HTTPS base URL, and OIDC or a strong local break-glass credential.
Use a protected Data Protection key ring for multi-instance AdminHost deployments. Details are in
`r11-production-hardening.md`.

ASP.NET Core maps double underscores in environment variables to configuration sections. A minimum production
starting point is:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:AllowedHosts = "portal.example.com;admin.example.com"
$env:ConnectionStrings__Default = "Host=db.internal;Port=5432;Database=openportalkit;Username=...;Password=..."
$env:OpenPortalKit__Persistence__PostgreSQL__Enabled = "true"
$env:OpenPortalKit__AgentAccess__PostgreSQL__Enabled = "true"
$env:OpenPortalKit__AgentAccess__OutputGeneration__PublicBaseUrl = "https://portal.example.com"
```

AdminHost additionally requires either OIDC settings or a generated local password hash. OIDC production settings
include `OpenPortalKit__AdminAuthentication__Mode=Oidc`, `Authority`, `ClientId`, a secret-supplied `ClientSecret`,
and `RequiredRole`. Generate a local break-glass hash with `tools/new-admin-password-hash.ps1`; never put the
plaintext password or resulting production hash in committed JSON. ApiHost and AdminHost require hostnames relevant
to their own listener, so separate service-level environment files are preferable to one shared file.

## Publication Acceptance

After deployment verify:

1. `/health/live` and `/health/ready` for the web hosts.
2. Administrator sign-in, role denial, antiforgery rejection, logout, and session revocation.
3. Draft save, review comment, approval, immediate publish, and scheduled publish through JobHost.
4. Public HTML plus JSON/Markdown representations where applicable.
5. `/sitemap.xml`, `/rss.xml`, `/llms.txt`, `/llms-full.txt`, and `/.well-known/agent.json`.
6. `./tools/opk check-agent-readiness --url https://portal.example.com`.
7. Backup restoration and the documented application/proxy rollback procedure.

The evidence-oriented release gate remains `release-checklist.md`.

## Known Gaps Before Turnkey Customer Delivery

- There is no one-command, checksummed multi-host Windows artifact.
- Windows Service/IIS registration, ACL provisioning, upgrade, and rollback are manual.
- The Windows Server and real-provider OIDC rehearsal still require target-environment evidence.
- Provider-specific object storage, CDN purge/distributed cache, and external search remain optional future adapters.

The remaining items are R15 Batches 3 and 4. They should not be represented as completed by a placeholder script or
no-op provider.
