# R9 Block Template System

R9 introduces reusable, server-rendered page template contracts. It is intentionally a Block Template System, not a general-purpose page builder.

## First Batch Scope

The Content module owns these generic contracts:

- `BlockDefinition` and `BlockSettingDefinition`
- `BlockInstance`
- `PageTemplate` and `PageTemplateStatus`
- `IPageTemplateStore`
- `PageTemplateService`

The predefined catalog includes `hero`, `rich-text`, `content-list`, `announcement-list`, `activity-list`, `report-list`, `data-table`, `chart`, `link-list`, `download-list`, `faq`, `contact`, and `embed`. Each block declares a schema version and a finite set of typed settings. Instances are flat and ordered; nested layout trees, custom CSS, script injection, and runtime-defined block types are not supported.

## Configuration and Versioning

Every block instance stores a JSON object plus its definition schema version. `PageTemplateValidator` rejects:

- unknown block definitions
- schema-version mismatches
- invalid or non-object JSON
- unknown configuration fields
- missing required settings
- invalid setting types
- duplicate instance identifiers or sort orders
- list sizes outside the bounded `1..50` range
- malformed chart, link, download, and FAQ list entries
- non-HTTPS embed URLs

`PageTemplateService` normalizes template codes, increments the template version on every update, and writes `block-template.created` or `block-template.updated` audit records. Every successful save records an immutable `PageTemplateVersion` snapshot alongside the current template.

## Storage

Migration `0010_block_templates.sql` creates `opk_page_templates` and `opk_page_template_versions`. `PostgresPageTemplateStore` writes the current template and its version snapshot in one database transaction. AdminHost selects this store automatically when `OpenPortalKit:Persistence:PostgreSQL:Enabled` is enabled; otherwise it uses the in-memory implementation for local development and tests.

## Rendering and Public Outputs

`PortalPage` fixes its source template identifier, template version, and ordered block snapshot. ApiHost exposes published pages at `GET /pages/{slug}`, Markdown snapshots at `GET /pages/{slug}.md`, and JSON snapshots at `GET /api/public/pages/{slug}.json`. All catalog blocks have server renderers. Content-oriented list blocks query only published content. `data-table` consumes a host-provided, read-only public-data adapter, so Content stays independent of the Data module. Structured list values and every rendered field are HTML encoded; rich text is rendered as encoded paragraphs rather than trusted raw HTML. Embeds require HTTPS and render in sandboxed iframes.

Published pages include title, description, canonical metadata, responsive public styling, and sitemap/RSS discovery. The same resolved body is supplied to HTML, Markdown, JSON, `llms.txt`, and `llms-full.txt`, preserving AgentSEO visibility for list and data content. Public output changes from template-backed pages flow through the existing audited publishing outbox.

`PortalPageService` creates draft pages from published templates, fixes the source template version and block snapshot, assigns new block instance identifiers, and records `portal-page.created`. Publishing records `portal-page.published` before a page becomes available to public queries. Durable page storage and audited publishing-outbox integration are already active.

Migration `0011_portal_pages.sql` and `PostgresPageStore` persist page instances when `OpenPortalKit:Persistence:PostgreSQL:Enabled` is enabled in AdminHost. The page row retains the source template version, independent serialized blocks, publishing state, and timestamps.

Migration `0012_portal_page_versions.sql` adds a monotonic page revision and immutable `opk_portal_page_versions` snapshots. Creating, editing, and publishing a page each retain the resulting snapshot. Published page edits keep the route available, record `portal-page.updated`, and emit a revision-keyed public-output event; published slugs remain immutable so an edit cannot silently break inbound links.

ApiHost uses the same persistence switch for public page reads. With the switch enabled in both hosts, a page created and published in AdminHost is rendered from the shared PostgreSQL store by `GET /pages/{slug}`.

## Publishing Delivery

Publishing a portal page writes a dedicated `PortalPagePublished` outbox event. The shared publishing revalidation handler now declares every event it consumes, including content and portal-page publication events. Page publication invalidates the page route and regenerates sitemap, RSS, llms.txt, and llms-full.txt through the durable JobHost pipeline, while preserving the page publication audit record.

## Admin Experience

The R9 visual direction is a dense operational template workspace: searchable template list, compact status/version columns, and an adjacent structured block outline. It is designed for repeated editorial work rather than freeform canvas editing. The first functional admin workflow follows once page-instance commands are available.

AdminHost provides `/Templates`: create a schema-guarded template from any predefined block combination, add the six generic initial template seeds, publish the template, create a page from a published template, and optionally publish the new page. Initial seeds cover corporate homepage, news portal, announcement center, activity portal, research portal, and data portal. Finance-oriented templates remain in the Finance Pack for R10. The forms invoke `PageTemplateService` and `PortalPageService`; they do not write state during `GET`.

`/Templates/Edit` is the versioned template editor. It supports metadata and status changes, adding and removing predefined blocks, explicit order values, schema-guided JSON configuration, immutable version history, and a server-rendered preview. `/Templates/PageEdit` provides the equivalent page workflow with revision history, draft publishing, and audited published-page updates. Preview output uses the same encoded renderer as ApiHost rather than a browser-only approximation.

ApiHost and AdminHost provide `IPageBlockDataResolver` adapters over `IDataSetStore` and `IDataRecordStore`. `DataTableBlock` therefore follows whichever Data storage implementation the host registers and does not depend on an ApiHost sample type or create a Content-to-Data module dependency.

## Boundaries

Block templates are generic Content-module composition contracts. `DataTableBlock` refers to datasets only by an opaque dataset reference; it does not own datasets or records. Industry-specific templates and block definitions belong in industry packs.
