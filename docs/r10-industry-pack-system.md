# R10 Industry Pack System

R10 introduces an industry-neutral loader for independently optional, declarative vertical packs. The core product does not depend on a reference pack and no pack depends on another pack.

## Reference Portfolio

The repository currently ships validated Finance, Technology, Education, and Entertainment definitions. Each pack declares content types, dataset schemas, publishing rules, page templates, dashboard cards, and traceable sample seed data.

The packs demonstrate publishing adaptation only. Technology is not a product-management system, Education is not an LMS or student system, Entertainment is not a streaming, rights, or ticketing system, and Finance is not a trading or financial business system.

## Manifest Contract

Every pack owns `pack.json` with identity, semantic version, minimum core version, registration capabilities, and explicit relative resource paths. `IndustryPackLoader` validates:

- required identity and version fields
- minimum core compatibility
- relative `.json` paths contained by the pack root
- duplicate and missing resources
- JSON object roots
- agreement between enabled registrations and declared resources
- SHA-256 checksums for the manifest and every resource

`IndustryPackCatalog` discovers pack directories, rejects duplicate names, and exposes a catalog only when every discovered pack is valid. Invalid declarations therefore fail closed before registration.

## Admin Impact

AdminHost validates the configured pack root during startup and exposes `/IndustryPacks`. The first-batch view is deliberately read-only: it shows available packs, versions, core requirements, resource counts, capabilities, and manifest checksum prefixes. No pack is enabled by default.

## Installation and Registration

Migration `0013_industry_pack_installations.sql` stores pack version, manifest checksum, enabled state, actor and timestamps, plus the checksum and kind of every registered resource. The in-memory store provides the same contract for development and tests.

`IndustryPackInstallationService` produces a dry-run plan with `Add`, `Update`, `Unchanged`, and `Remove` changes. Enablement validates every target before applying changes, invokes targets only for added or changed resources, saves the resulting checksum set, and records `industry-pack.enabled`. Repeated enablement with unchanged resources performs no target writes. Disablement records `industry-pack.disabled` and retains resource state and published data rather than destructively uninstalling it.

AdminHost adapters register pack templates through `PageTemplateService` and register dataset definitions and schema versions through `IDataSetStore`. Template and dataset codes are namespaced by pack to prevent cross-pack collisions. Content type, rule, dashboard-card, and seed definitions enter the host runtime registry for downstream module consumers. Registration remains host orchestration; reference-pack concepts stay in `industry-packs/{PackName}`.

`/IndustryPacks` now supports dry-run, enable, upgrade-by-checksum, and disable workflows. It displays current state and the complete impact plan before an operator changes pack state.

## Runtime Lifecycle

Enabled state and resource checksums are durable; resource payloads remain owned by the versioned pack files. On startup, AdminHost replays enabled runtime definitions from those files. Startup fails closed when an enabled pack is missing or its manifest checksum differs from the installed checksum, preventing an unreviewed pack change from silently entering the running product.

Templates and datasets retain the records created during enablement. In-memory dataset definitions are replayed after restart, while durable templates are not duplicated. Content types, rules, dashboard cards, and seed definitions are restored into the host registry. Disabling a pack removes its runtime contributions immediately without deleting previously published content or imported source data.

## Dashboard Impact

Enabled dashboard-card definitions are exposed through the standard `IDashboardSignalSource` aggregation path. Each contribution is namespaced by pack, identifies `IndustryPacks` as its source module, and reports that its declared source metric is active. These cards describe registration health; they do not fabricate business measurements. A forced Dashboard refresh reflects enable and disable operations without restarting the host.

## Operational Verification

The PostgreSQL lifecycle was verified through the real AdminHost workflow: enable Education, restart the host, rehydrate five runtime resources, expose three Education Dashboard cards, disable the pack, and observe zero remaining industry-pack cards. The installation remains as a disabled audit record.
