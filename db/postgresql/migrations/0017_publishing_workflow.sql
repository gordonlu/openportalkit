-- OpenPortalKit R14 durable publishing workflow and review evidence.

create table if not exists opk_publishing_workflow_items (
    id uuid primary key,
    target_type text not null,
    target_id text not null,
    state text not null,
    version_number integer not null,
    created_by uuid not null,
    updated_by uuid not null,
    created_at timestamptz not null,
    updated_at timestamptz not null,
    review_comment text null,
    approved_at timestamptz null,
    published_at timestamptz null,
    scheduled_at timestamptz null,
    archived_at timestamptz null,
    constraint uq_opk_publishing_workflow_target unique (target_type, target_id),
    constraint ck_opk_publishing_workflow_state check (
        state in ('Draft', 'Review', 'Approved', 'Published', 'Rejected', 'Archived')),
    constraint ck_opk_publishing_workflow_version check (version_number > 0)
);

create index if not exists ix_opk_publishing_workflow_due
    on opk_publishing_workflow_items (scheduled_at, id)
    where state = 'Approved' and scheduled_at is not null;

create table if not exists opk_approval_records (
    id uuid primary key,
    workflow_item_id uuid not null references opk_publishing_workflow_items(id) on delete cascade,
    target_type text not null,
    target_id text not null,
    actor_id uuid not null,
    action text not null,
    from_state text not null,
    to_state text not null,
    comment text null,
    occurred_at timestamptz not null,
    constraint ck_opk_approval_action check (action in ('Approve', 'Reject', 'RequestChanges'))
);

create index if not exists ix_opk_approval_records_target_occurred
    on opk_approval_records (target_type, target_id, occurred_at desc);

comment on table opk_publishing_workflow_items is
    'Industry-neutral review, scheduling, publication, and archive state for auditable public-output targets.';
comment on table opk_approval_records is
    'Immutable approval, rejection, and requested-change evidence with reviewer comments.';
