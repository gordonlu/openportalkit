-- R11 query-performance indexes verified against production store access patterns.

-- Supports global analytics pagination and retention deletion by occurred_at.
create index if not exists ix_opk_analytics_events_occurred_id
    on opk_analytics_events (occurred_at desc, id desc);

-- Supports event-type-only analytics filtering without requiring a site filter.
create index if not exists ix_opk_analytics_events_type_occurred_id
    on opk_analytics_events (event_type, occurred_at desc, id desc);

-- Supports the public page read path without scanning draft and archived rows.
create index if not exists ix_opk_portal_pages_public_site_title
    on opk_portal_pages (site_id, title, id)
    include (published_at)
    where status = 'Published';
