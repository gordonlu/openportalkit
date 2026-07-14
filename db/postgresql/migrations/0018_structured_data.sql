-- OpenPortalKit durable structured-data storage.
-- Records preserve source, import batch, as-of date, schema version, checksum, and timestamps.

create table if not exists opk_data_sets (
    id uuid primary key,
    site_id uuid not null,
    code text not null,
    name text not null,
    description text not null,
    is_public boolean not null default false,
    created_at timestamptz not null,
    updated_at timestamptz not null,
    constraint uq_opk_data_sets_site_code unique (site_id, code),
    constraint ck_opk_data_sets_code check (code <> ''),
    constraint ck_opk_data_sets_name check (name <> '')
);

create index if not exists ix_opk_data_sets_public_catalog
    on opk_data_sets (site_id, name, id)
    where is_public;

comment on table opk_data_sets is
    'Industry-neutral structured dataset catalogue with explicit public visibility.';

create table if not exists opk_data_schema_versions (
    id uuid primary key,
    data_set_id uuid not null references opk_data_sets(id) on delete cascade,
    version_number integer not null,
    schema_json jsonb not null,
    checksum text not null,
    created_at timestamptz not null,
    constraint uq_opk_data_schema_versions_number unique (data_set_id, version_number),
    constraint ck_opk_data_schema_versions_number check (version_number > 0),
    constraint ck_opk_data_schema_versions_schema check (jsonb_typeof(schema_json) = 'object'),
    constraint ck_opk_data_schema_versions_checksum check (checksum ~ '^[0-9a-f]{16}$')
);

create index if not exists ix_opk_data_schema_versions_latest
    on opk_data_schema_versions (data_set_id, version_number desc);

comment on table opk_data_schema_versions is
    'Immutable versioned JSON schemas used to validate and explain structured records.';

create table if not exists opk_data_records (
    id uuid primary key,
    data_set_id uuid not null references opk_data_sets(id) on delete cascade,
    record_key text not null,
    payload_json jsonb not null,
    as_of_date date not null,
    schema_version_id uuid not null references opk_data_schema_versions(id),
    source_batch_id uuid not null,
    source text not null,
    checksum text not null,
    created_at timestamptz not null,
    updated_at timestamptz not null,
    constraint uq_opk_data_records_key unique (data_set_id, record_key),
    constraint ck_opk_data_records_key check (record_key <> ''),
    constraint ck_opk_data_records_source check (source <> ''),
    constraint ck_opk_data_records_checksum check (checksum ~ '^[0-9a-f]{16}$')
);

create index if not exists ix_opk_data_records_catalog
    on opk_data_records (data_set_id, record_key);

create index if not exists ix_opk_data_records_traceability
    on opk_data_records (source_batch_id, as_of_date, schema_version_id);

comment on table opk_data_records is
    'Structured records with mandatory provenance, schema, freshness, checksum, and timestamps.';
