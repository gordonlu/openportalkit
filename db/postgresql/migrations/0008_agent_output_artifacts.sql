-- OpenPortalKit R8 Agent Access / AgentSEO
-- PostgreSQL baseline schema for traceable public agent output artifacts.

create table if not exists opk_agent_output_artifacts (
    path text primary key,
    content_type text not null,
    body text not null,
    source_id text not null,
    source_kind text not null,
    schema_version text not null,
    checksum text not null,
    generated_at timestamptz not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint ck_opk_agent_output_artifacts_path
        check (path <> '' and left(path, 1) = '/'),
    constraint ck_opk_agent_output_artifacts_source
        check (source_id <> '' and source_kind <> ''),
    constraint ck_opk_agent_output_artifacts_checksum
        check (checksum <> ''),
    constraint ck_opk_agent_output_artifacts_schema
        check (schema_version <> '')
);

create index if not exists ix_opk_agent_output_artifacts_source
    on opk_agent_output_artifacts (source_kind, source_id);

create index if not exists ix_opk_agent_output_artifacts_generated
    on opk_agent_output_artifacts (generated_at desc);

create index if not exists ix_opk_agent_output_artifacts_checksum
    on opk_agent_output_artifacts (checksum);

comment on table opk_agent_output_artifacts is
    'Traceable generated public outputs for AgentSEO, including Markdown snapshots, JSON snapshots, llms files, and agent manifests.';

comment on column opk_agent_output_artifacts.checksum is
    'Stable checksum of the generated body used to detect unchanged public agent output.';
