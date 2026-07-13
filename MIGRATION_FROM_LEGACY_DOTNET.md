# Migration From Legacy .NET

OpenPortalKit includes the first production migration-toolkit workflow for legacy ASP.NET MVC, EF6, and other content-heavy systems.

Use `opk import legacy` for a repeatable offline dry run before opening AdminHost. A runnable fixture and exact
command are provided in `examples/legacy-migration`. The CLI uses the same analyzer as AdminHost, writes the report
atomically, refuses to overwrite existing evidence, and returns exit code `1` when blocking issues exist.

## Implemented Analysis and Staging

AdminHost exposes `/Migration` for authenticated operators. It accepts an in-memory UTF-8 CSV up to 5 MB and never writes content during analysis or staging. Required columns are:

```txt
source_id,title,slug,summary,body,old_url,asset_paths
```

The report preserves source name, import batch, as-of date, schema version, source checksum, and analysis timestamp. Blocking findings include missing required fields, non-canonical or duplicate slugs, duplicate source IDs, duplicate old URLs, redirect loops, and referenced assets absent from the supplied inventory. Exact duplicate content is reported as a warning for editorial review.

`Validate and stage` reads and validates the upload again, then persists the complete report and its trace fields in `opk_legacy_migration_batches`. The `(source, import batch)` identity is immutable: an identical checksum is idempotent, while changed input or a rolled-back batch requires a new import batch. Staging and rollback record the operator and timestamp in the batch journal and emit global audit events. Rollback retains the report; it does not delete migration evidence.

Authenticated operators can download the persisted report as non-cacheable JSON from the staged-batch list. The export includes candidates, redirects, row-level warnings and errors, counts, and all source trace fields.

Staging does not create Content items, redirects, assets, search documents, feeds, sitemaps, or AgentSEO output. Those writes belong to the future controlled promotion transaction.

## Legacy MVC and EF6 Extraction

Run extraction against a read-only database login and a stable as-of snapshot. Do not point migration tooling at an EF6 production database with write credentials. Shape the query into the CSV contract and preserve the legacy primary key as `source_id`; never regenerate source identities during retries.

For SQL Server, export through a small reviewed application or script using `Microsoft.Data.SqlClient` and a real CSV writer. Do not concatenate fields with commas in SQL because bodies, titles, and URLs can contain delimiters, quotes, and newlines. The extraction query should be deterministic, ordered by the legacy primary key, and should return the original route and attachment paths without rewriting them.

Record together with every export:

- source database and schema name
- extraction query revision
- UTC extraction timestamp and business as-of date
- row count and SHA-256 of the final CSV
- attachment inventory root and inventory checksum
- operator and approved change ticket

## Route and Attachment Mapping

Place each old public route in `old_url`. Dry run canonicalizes the new slug and reports the exact old-to-new mapping. Duplicate old URLs and redirect loops are blocking findings. Keep the old host out of the field; route mappings are path based and can be validated before DNS cutover.

`asset_paths` contains source attachment references separated by `|`. Generate the available-asset inventory from a read-only copy of the legacy attachment root. A reference absent from that inventory blocks staging. R11 does not copy binaries: the future storage adapter must validate each file with `AssetUploadValidator`, generate an opaque storage key, and retain source path, checksum, size, verified MIME, batch, and timestamps.

## Staged Migration and Parallel Run

1. Freeze legacy schema changes and create the read-only extraction account.
2. Export a representative batch, run Analyze, and resolve every blocking issue.
3. Re-select the unchanged source file and use Validate and stage.
4. Download and archive the JSON report with the source CSV checksum.
5. Run the legacy site and OpenPortalKit public outputs in parallel on separate hostnames.
6. Compare route maps, content counts, attachment inventory, sitemap, RSS, JSON/Markdown snapshots, and AgentSEO outputs.
7. Keep DNS and reverse-proxy cutover outside the application deployment so traffic can be returned independently.

This is a strangler migration: move a bounded route group only after its report and public outputs pass review. Do not switch all legacy routes merely because one batch staged successfully.

## Rollback

Before public cutover, rollback marks the staged batch as `RolledBack` and retains its immutable report and audit history. After traffic cutover, operational rollback means restoring proxy/DNS routing to the legacy site; staging rollback alone does not change public traffic.

Controlled promotion into Content, Redirect, Asset, Search, and AgentSEO stores remains intentionally unavailable until those writes share a PostgreSQL transaction and rollback journal. A rolled-back or changed source must use a new import batch. Never delete or rewrite a staged report to reuse an identifier.
