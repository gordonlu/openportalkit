-- OpenPortalKit R9 Block Template System.
-- Versioned generic templates with ordered, schema-versioned block instance snapshots.

create table if not exists opk_page_templates (
    id uuid primary key,
    code text not null unique,
    name text not null,
    description text not null,
    status text not null,
    version integer not null,
    blocks_json jsonb not null,
    created_by uuid not null,
    updated_by uuid not null,
    created_at timestamptz not null,
    updated_at timestamptz not null,
    constraint ck_opk_page_templates_code check (code <> ''),
    constraint ck_opk_page_templates_name check (name <> ''),
    constraint ck_opk_page_templates_status check (status in ('Draft', 'Published', 'Archived')),
    constraint ck_opk_page_templates_version check (version > 0),
    constraint ck_opk_page_templates_blocks_array check (jsonb_typeof(blocks_json) = 'array')
);

create index if not exists ix_opk_page_templates_status_updated
    on opk_page_templates (status, updated_at desc);

create table if not exists opk_page_template_versions (
    template_id uuid not null references opk_page_templates(id) on delete restrict,
    version integer not null,
    snapshot_json jsonb not null,
    created_by uuid not null,
    created_at timestamptz not null,
    primary key (template_id, version),
    constraint ck_opk_page_template_versions_version check (version > 0),
    constraint ck_opk_page_template_versions_snapshot_object check (jsonb_typeof(snapshot_json) = 'object')
);

create index if not exists ix_opk_page_template_versions_created
    on opk_page_template_versions (created_at desc);

comment on table opk_page_templates is
    'Current version of generic page templates composed from predefined server-rendered blocks.';

comment on table opk_page_template_versions is
    'Immutable template snapshots used for page-instance traceability and rollback.';
