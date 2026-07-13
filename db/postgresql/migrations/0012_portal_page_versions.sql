-- OpenPortalKit R9 Portal Page Version History.

alter table opk_portal_pages
    add column if not exists revision integer not null default 1;

alter table opk_portal_pages
    drop constraint if exists ck_opk_portal_pages_revision;

alter table opk_portal_pages
    add constraint ck_opk_portal_pages_revision check (revision > 0);

create table if not exists opk_portal_page_versions (
    page_id uuid not null references opk_portal_pages(id) on delete cascade,
    revision integer not null,
    snapshot_json jsonb not null,
    created_by uuid not null,
    created_at timestamptz not null,
    primary key (page_id, revision),
    constraint ck_opk_portal_page_versions_revision check (revision > 0),
    constraint ck_opk_portal_page_versions_snapshot check (jsonb_typeof(snapshot_json) = 'object')
);

create index if not exists ix_opk_portal_page_versions_created
    on opk_portal_page_versions (page_id, created_at desc);

comment on table opk_portal_page_versions is
    'Immutable portal page snapshots created by audited page mutations.';
