-- OpenPortalKit R8 durable publishing delivery.
-- Shared outbox, idempotency, public-output revalidation, and audit records for JobHost processing.

create table if not exists opk_outbox_messages (
    id uuid primary key,
    event_name text not null,
    payload_json jsonb not null,
    idempotency_key text not null unique,
    occurred_at timestamptz not null,
    processed_at timestamptz null,
    attempt_count integer not null default 0,
    last_error text null,
    lease_expires_at timestamptz null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint ck_opk_outbox_messages_event_name check (event_name <> ''),
    constraint ck_opk_outbox_messages_idempotency_key check (idempotency_key <> ''),
    constraint ck_opk_outbox_messages_attempt_count check (attempt_count >= 0),
    constraint ck_opk_outbox_messages_payload_object check (jsonb_typeof(payload_json) = 'object')
);

create index if not exists ix_opk_outbox_messages_pending
    on opk_outbox_messages (occurred_at, id)
    where processed_at is null;

create index if not exists ix_opk_outbox_messages_lease
    on opk_outbox_messages (lease_expires_at)
    where processed_at is null;

create table if not exists opk_idempotency_keys (
    idempotency_key text primary key,
    processed_at timestamptz not null,
    created_at timestamptz not null default now(),
    constraint ck_opk_idempotency_keys_key check (idempotency_key <> '')
);

create table if not exists opk_public_output_revalidations (
    id uuid primary key,
    source_event_name text not null,
    source_idempotency_key text not null unique,
    started_at timestamptz not null,
    completed_at timestamptz not null,
    invalidated_routes_json jsonb not null default '[]'::jsonb,
    regenerated_outputs_json jsonb not null default '[]'::jsonb,
    succeeded boolean not null,
    error text null,
    created_at timestamptz not null default now(),
    constraint ck_opk_public_output_revalidations_event_name check (source_event_name <> ''),
    constraint ck_opk_public_output_revalidations_key check (source_idempotency_key <> ''),
    constraint ck_opk_public_output_revalidations_completed check (completed_at >= started_at),
    constraint ck_opk_public_output_revalidations_routes_array check (jsonb_typeof(invalidated_routes_json) = 'array'),
    constraint ck_opk_public_output_revalidations_outputs_array check (jsonb_typeof(regenerated_outputs_json) = 'array')
);

create index if not exists ix_opk_public_output_revalidations_started
    on opk_public_output_revalidations (started_at desc);

create table if not exists opk_audit_logs (
    id uuid primary key,
    actor_id uuid null,
    action text not null,
    target_type text not null,
    target_id text not null,
    summary text null,
    metadata_json jsonb null,
    occurred_at timestamptz not null,
    created_at timestamptz not null default now(),
    constraint ck_opk_audit_logs_action check (action <> ''),
    constraint ck_opk_audit_logs_target_type check (target_type <> ''),
    constraint ck_opk_audit_logs_target_id check (target_id <> ''),
    constraint ck_opk_audit_logs_metadata_object check (
        metadata_json is null or jsonb_typeof(metadata_json) = 'object'
    )
);

create index if not exists ix_opk_audit_logs_actor_occurred
    on opk_audit_logs (actor_id, occurred_at desc)
    where actor_id is not null;

create index if not exists ix_opk_audit_logs_target_occurred
    on opk_audit_logs (target_type, target_id, occurred_at desc);

comment on table opk_outbox_messages is
    'Durable integration events with lease-based claim semantics for horizontally scaled workers.';

comment on table opk_idempotency_keys is
    'Successfully completed outbox idempotency keys, retained to prevent duplicate public-output changes.';

comment on table opk_public_output_revalidations is
    'Auditable public-output revalidation results, including regenerated AgentSEO snapshots.';

comment on table opk_audit_logs is
    'Queryable audit history for public-output-changing actions and administrative operations.';
