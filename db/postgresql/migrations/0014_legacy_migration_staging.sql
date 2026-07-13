create table if not exists opk_legacy_migration_batches (
    id uuid primary key,
    source text not null check (length(trim(source)) > 0),
    import_batch text not null check (length(trim(import_batch)) > 0),
    as_of_date date not null,
    schema_version text not null check (length(trim(schema_version)) > 0),
    source_checksum character(64) not null check (source_checksum ~ '^[0-9A-Fa-f]{64}$'),
    report_json jsonb not null,
    total_rows integer not null check (total_rows >= 0),
    valid_rows integer not null check (valid_rows >= 0 and valid_rows <= total_rows),
    error_count integer not null check (error_count >= 0),
    warning_count integer not null check (warning_count >= 0),
    status text not null check (status in ('Staged', 'RolledBack')),
    staged_by uuid not null,
    staged_at timestamp with time zone not null,
    rolled_back_by uuid null,
    rolled_back_at timestamp with time zone null,
    constraint uq_opk_legacy_migration_source_batch unique (source, import_batch),
    constraint ck_opk_legacy_migration_rollback check (
        (status = 'Staged' and rolled_back_by is null and rolled_back_at is null) or
        (status = 'RolledBack' and rolled_back_by is not null and rolled_back_at is not null))
);

create index if not exists ix_opk_legacy_migration_batches_staged_at
    on opk_legacy_migration_batches (staged_at desc);
