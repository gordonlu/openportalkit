# R9 Block Template System

R9 introduces reusable, server-rendered page template contracts. It is intentionally a Block Template System, not a general-purpose page builder.

## First Batch Scope

The Content module owns these generic contracts:

- `BlockDefinition` and `BlockSettingDefinition`
- `BlockInstance`
- `PageTemplate` and `PageTemplateStatus`
- `IPageTemplateStore`
- `PageTemplateService`

The predefined catalog starts with `hero`, `rich-text`, `content-list`, and `data-table`. Each block declares a schema version and a finite set of typed settings. Instances are flat and ordered; nested layout trees, custom CSS, script injection, and runtime-defined block types are not supported.

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

`PageTemplateService` normalizes template codes, increments the template version on every update, and writes `block-template.created` or `block-template.updated` audit records. Every successful save records an immutable `PageTemplateVersion` snapshot alongside the current template.

## Storage

Migration `0010_block_templates.sql` creates `opk_page_templates` and `opk_page_template_versions`. `PostgresPageTemplateStore` writes the current template and its version snapshot in one database transaction. AdminHost selects this store automatically when `OpenPortalKit:Persistence:PostgreSQL:Enabled` is enabled; otherwise it uses the in-memory implementation for local development and tests.

## Rendering and Public Outputs

`PortalPage` fixes its source template identifier, template version, and ordered block snapshot. ApiHost exposes published pages at `GET /pages/{slug}`, Markdown snapshots at `GET /pages/{slug}.md`, and JSON snapshots at `GET /api/public/pages/{slug}.json`. The first server renderers cover `hero` and `rich-text`; all values are HTML encoded before output, and rich text is rendered as encoded paragraphs rather than trusted raw HTML. Unsupported block definitions fail closed until their server renderer is registered.

Published pages include title, description, and canonical metadata and are added to sitemap and RSS discovery. The next batch will add `content-list` and `data-table` renderers, page persistence, page publish commands, and AgentSEO snapshots. Public output changes from template-backed pages will then flow through the existing audited publishing outbox.

`PortalPageService` now creates draft pages from published templates, fixes the source template version and block snapshot, assigns new block instance identifiers, and records `portal-page.created`. Publishing records `portal-page.published` before a page becomes available to public queries. Durable page storage and audited publishing-outbox integration are the next step before exposing page editing commands in AdminHost.

Migration `0011_portal_pages.sql` and `PostgresPageStore` persist page instances when `OpenPortalKit:Persistence:PostgreSQL:Enabled` is enabled in AdminHost. The page row retains the source template version, independent serialized blocks, publishing state, and timestamps.

ApiHost uses the same persistence switch for public page reads. With the switch enabled in both hosts, a page created and published in AdminHost is rendered from the shared PostgreSQL store by `GET /pages/{slug}`.

## Publishing Delivery

Publishing a portal page writes a dedicated `PortalPagePublished` outbox event. The shared publishing revalidation handler now declares every event it consumes, including content and portal-page publication events. Page publication invalidates the page route and regenerates sitemap, RSS, llms.txt, and llms-full.txt through the durable JobHost pipeline, while preserving the page publication audit record.

## Admin Experience

The R9 visual direction is a dense operational template workspace: searchable template list, compact status/version columns, and an adjacent structured block outline. It is designed for repeated editorial work rather than freeform canvas editing. The first functional admin workflow follows once page-instance commands are available.

AdminHost provides `/Templates`: create a schema-guarded Hero/Rich Text template, publish the template, create a page from a published template, and optionally publish the new page. The forms invoke `PageTemplateService` and `PortalPageService`; they do not write state during `GET`.

## Boundaries

Block templates are generic Content-module composition contracts. `DataTableBlock` refers to datasets only by an opaque dataset reference; it does not own datasets or records. Industry-specific templates and block definitions belong in industry packs.
