# R7 Dashboard + Analytics

R7 adds operational dashboard aggregation, privacy-conscious analytics, cached dashboard snapshots, and metrics export.

## Runtime Flow

1. First-party analytics events are captured through `POST /analytics/events`.
2. Events are normalized before persistence:
   - raw session identifiers are hashed
   - IP addresses are anonymized
   - bot traffic is classified
   - third-party cookies and cross-site tracking are not required
3. Dashboard signal sources aggregate from module-owned stores.
4. `DashboardSummaryService` writes a cached snapshot with a checksum and TTL.
5. Admin UI reads `GET /admin/dashboard/summary`.
6. Observability systems can read `GET /admin/dashboard/metrics.prometheus` or subscribe to the `.NET` meter named `OpenPortalKit.Dashboard`.

## Boundaries

Dashboard aggregates signals from other modules. It does not own content, dataset, workflow, search, or job business state.

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

- Site operations: analytics events, page views, unique visitors, bot traffic, 404s, downloads, form submissions.
- Content: draft/review/rejected/published/scheduled/archived/stale/readiness counts.
- Data publishing: dataset counts, record counts, stale dataset and source attribution checks.
- System health: outbox backlog and oldest pending age.
- Privacy: no cross-site tracking by default, no third-party cookies by default, hashed sessions, anonymized IPs, retention pruning.
- Observability: Prometheus text export and .NET meter publishing.
