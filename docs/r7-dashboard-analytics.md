# R7 Dashboard + Analytics

R7 adds operational dashboard aggregation, privacy-conscious analytics, AgentSEO readiness signals, system dependency health, cached dashboard snapshots, and metrics export.

## Runtime Flow

1. First-party analytics events are captured through `POST /analytics/events`.
2. Events are normalized before persistence:
   - raw session identifiers are hashed
   - IP addresses are anonymized
   - bot traffic is classified
   - third-party cookies and cross-site tracking are not required
3. Public API and public SEO outputs enqueue generic `api_request` runtime events with status and latency metadata, then a background service persists them.
4. Public pages can include `GET /analytics/client.js` to send privacy-conscious `page_view` events without cookies.
5. Dashboard signal sources aggregate from module-owned stores.
6. `DashboardSummaryService` writes a cached snapshot with a checksum and TTL.
7. Admin UI reads `GET /admin/dashboard/summary` and renders dashboard cards from the live summary.
8. Observability systems can read `GET /admin/dashboard/metrics.prometheus` or subscribe to the `.NET` meter named `OpenPortalKit.Dashboard`.

## Boundaries

Dashboard aggregates signals from other modules. It does not own content, dataset, workflow, search, or job business state.

Agent readiness is provided through `IAgentReadinessSignalProvider`. The default AdminHost registration uses `ContentAgentReadinessSignalProvider`, which derives page readiness from published content without storing dashboard-owned business state. The dashboard source only summarizes page readiness signals and analytics events.

System health is provided through `IDashboardHealthProbe`. The dashboard source only summarizes dependency probe results.

Runtime health is summarized from first-party analytics events with generic event types such as `api_request`, `background_job`, `search_indexing`, `snapshot_generation`, and `public_output_revalidation`.

Public request analytics capture is non-blocking in ApiHost: the middleware creates an event and writes it to a bounded in-process queue, while a hosted background service persists events and performs retention cleanup.

Industry-specific dashboard cards must live in industry packs. Core R7 tables and code remain portal-generic.

## PostgreSQL Migration

Baseline PostgreSQL schema is in:

```txt
db/postgresql/migrations/0007_dashboard_analytics.sql
```

The migration creates:

- `opk_analytics_events`
- `opk_dashboard_snapshots`

The analytics table intentionally stores `hashed_session_id` and `anonymized_ip_address`, not raw session ids or raw IP addresses.

The snapshot table stores `source_checksum`, `schema_version`, `summary_json`, `source_modules_json`, and TTL timestamps so cached summaries can be reused without slowing public pages.

## PostgreSQL Store Adapter

Dashboard includes provider-agnostic PostgreSQL adapters:

- `PostgresAnalyticsEventStore`
- `PostgresDashboardSnapshotStore`
- `DashboardPostgresConnectionFactory`

The adapters use `System.Data.Common` and PostgreSQL SQL. They do not reference a provider package directly. Production hosts should register an ADO.NET provider such as Npgsql and enable the adapter with configuration:

```json
{
  "OpenPortalKit": {
    "Dashboard": {
      "PostgreSQL": {
        "Enabled": true,
        "ProviderInvariantName": "Npgsql",
        "ConnectionStringName": "Default"
      }
    }
  }
}
```

The default development configuration leaves the adapter disabled and uses in-memory stores so the solution can build and run without external packages.

## Admin Endpoints

- `GET /admin/dashboard/summary`
- `GET /admin/dashboard/snapshot`
- `GET /admin/dashboard/metrics.prometheus`
- `GET /admin/analytics/privacy`
- `GET /admin/analytics/events`
- `POST /analytics/events`

## Public Analytics Endpoints

- `GET /analytics/client.js`
- `POST /analytics/events`

The browser client uses `localStorage` for a first-party session identifier and does not use cookies.

## Configuration

```json
{
  "OpenPortalKit": {
    "Dashboard": {
      "SnapshotTtlSeconds": 60,
      "MaxSnapshotTtlSeconds": 300
    },
    "AnalyticsPrivacy": {
      "AnonymizeIpAddresses": true,
      "RetentionDays": 180,
      "AllowCrossSiteTracking": false,
      "AllowThirdPartyCookies": false
    }
  }
}
```

## Acceptance Coverage

- Site operations: analytics events, page views, unique visitors, bot traffic, 404s, downloads, form submissions, activity registrations, top page/section counts, traffic source counts, search keyword counts, entry/exit page counts, and slow page counts.
- Content: draft/review/rejected/published/scheduled/archived/stale counts, SEO metadata gaps, summary/cover/AgentSEO snapshot gaps, and top content type/author/category counts.
- Data publishing: dataset counts, record counts, import batch/success/failure counts, quality failure counts, stale dataset checks, source attribution checks, as-of-date status, latest snapshot status, dataset API/export request counts, and top dataset counts.
- Agent readiness: average readiness score, low-score pages, Markdown/JSON snapshot gaps, sitemap coverage, llms.txt coverage, structured data coverage, public OpenAPI status, AI bot traffic, blocked training bot requests, agent-facing errors, and top pages accessed by agents.
- System health: API latency/error rate, background job success/failure, search indexing lag, snapshot generation failures, revalidation failures, outbox backlog, oldest pending age, configured database/cache/search/storage health probes, unhealthy/degraded dependency counts, and dependency latency.
- Privacy: no cross-site tracking by default, no third-party cookies by default, hashed sessions, anonymized IPs, retention pruning.
- Observability: Prometheus text export and .NET meter publishing.
