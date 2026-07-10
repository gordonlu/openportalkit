-- OpenPortalKit R9 Portal Pages.
-- Published page instances retain their source template version and block snapshot.

create table if not exists opk_portal_pages (
    id uuid primary key,
    site_id uuid not null,
    template_id uuid not null references opk_page_templates(id) on delete restrict,
    template_version integer not null,
    title text not null,
    slug text not null,
    summary text not null,
    status text not null,
    blocks_json jsonb not null,
    created_by uuid not null,
    updated_by uuid not null,
    created_at timestamptz not null,
    updated_at timestamptz not null,
    published_at timestamptz null,
    constraint uq_opk_portal_pages_site_slug unique (site_id, slug),
    constraint ck_opk_portal_pages_template_version check (template_version > 0),
    constraint ck_opk_portal_pages_title check (title <> ''),
    constraint ck_opk_portal_pages_slug check (slug <> ''),
    constraint ck_opk_portal_pages_summary check (summary <> ''),
    constraint ck_opk_portal_pages_status check (status in ('Draft', 'Published', 'Archived')),
    constraint ck_opk_portal_pages_blocks_array check (jsonb_typeof(blocks_json) = 'array'),
    constraint ck_opk_portal_pages_publication check (
        (status = 'Published' and published_at is not null) or
        (status <> 'Published')
    )
);

create index if not exists ix_opk_portal_pages_site_status_updated
    on opk_portal_pages (site_id, status, updated_at desc);

create index if not exists ix_opk_portal_pages_published
    on opk_portal_pages (published_at desc)
    where status = 'Published';

comment on table opk_portal_pages is
    'Public page instances generated from versioned templates with independent ordered block snapshots.';
