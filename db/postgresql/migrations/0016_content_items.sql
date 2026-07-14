-- OpenPortalKit R14 content inventory persistence.
-- Content remains industry-neutral and retains authoring and publication traceability.

create table if not exists opk_content_items (
    id uuid primary key,
    site_id uuid not null,
    content_type_id uuid not null,
    title text not null,
    slug text not null,
    summary text not null,
    body text not null,
    cover_asset_id uuid null,
    status text not null,
    category_id uuid null,
    tags_json jsonb not null default '[]'::jsonb,
    author_id uuid null,
    source text null,
    published_at timestamptz null,
    scheduled_at timestamptz null,
    expires_at timestamptz null,
    created_by uuid not null,
    updated_by uuid not null,
    created_at timestamptz not null,
    updated_at timestamptz not null,
    constraint uq_opk_content_items_site_slug unique (site_id, slug),
    constraint ck_opk_content_items_title check (title <> ''),
    constraint ck_opk_content_items_slug check (slug <> ''),
    constraint ck_opk_content_items_status check (
        status in ('Draft', 'Review', 'Approved', 'Published', 'Rejected', 'Archived')),
    constraint ck_opk_content_items_tags_array check (jsonb_typeof(tags_json) = 'array'),
    constraint ck_opk_content_items_publication check (
        (status = 'Published' and published_at is not null) or status <> 'Published'),
    constraint ck_opk_content_items_schedule check (
        scheduled_at is null or expires_at is null or scheduled_at < expires_at)
);

create index if not exists ix_opk_content_items_site_status_updated
    on opk_content_items (site_id, status, updated_at desc, id);

create index if not exists ix_opk_content_items_type_updated
    on opk_content_items (content_type_id, updated_at desc, id);

create index if not exists ix_opk_content_items_author_updated
    on opk_content_items (author_id, updated_at desc, id)
    where author_id is not null;

create index if not exists ix_opk_content_items_tags
    on opk_content_items using gin (tags_json);

create index if not exists ix_opk_content_items_publication_window
    on opk_content_items (published_at desc, expires_at)
    where status = 'Published';

comment on table opk_content_items is
    'Industry-neutral authored content with publication state, provenance, and timestamps.';

create table if not exists opk_content_item_versions (
    content_item_id uuid not null references opk_content_items(id) on delete cascade,
    revision integer not null,
    snapshot_json jsonb not null,
    created_by uuid not null,
    created_at timestamptz not null,
    primary key (content_item_id, revision),
    constraint ck_opk_content_item_versions_revision check (revision > 0),
    constraint ck_opk_content_item_versions_snapshot check (jsonb_typeof(snapshot_json) = 'object')
);

create index if not exists ix_opk_content_item_versions_created
    on opk_content_item_versions (content_item_id, created_at desc);

comment on table opk_content_item_versions is
    'Immutable full content snapshots retained for authoring history and recovery.';
