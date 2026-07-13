-- OpenPortalKit R10 Industry Pack Installation State.

create table if not exists opk_industry_pack_installations (
    pack_name text primary key,
    version text not null,
    manifest_checksum text not null,
    is_enabled boolean not null,
    updated_by uuid not null,
    installed_at timestamptz not null,
    updated_at timestamptz not null,
    constraint ck_opk_industry_pack_name check (pack_name <> ''),
    constraint ck_opk_industry_pack_version check (version <> ''),
    constraint ck_opk_industry_pack_manifest_checksum check (length(manifest_checksum) = 64)
);

create table if not exists opk_industry_pack_resources (
    pack_name text not null references opk_industry_pack_installations(pack_name) on delete cascade,
    resource_path text not null,
    resource_kind text not null,
    checksum text not null,
    registered_at timestamptz not null,
    primary key (pack_name, resource_path),
    constraint ck_opk_industry_pack_resource_path check (resource_path <> ''),
    constraint ck_opk_industry_pack_resource_kind check (resource_kind in (
        'ContentType', 'DataSet', 'Template', 'Rule', 'DashboardCard', 'SeedData')),
    constraint ck_opk_industry_pack_resource_checksum check (length(checksum) = 64)
);

create index if not exists ix_opk_industry_pack_installations_enabled
    on opk_industry_pack_installations (is_enabled, pack_name);

comment on table opk_industry_pack_installations is
    'Audited industry pack enablement state keyed by stable pack name.';
comment on table opk_industry_pack_resources is
    'Registered pack resource checksums used for idempotent updates and dry-run impact plans.';
