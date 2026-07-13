param(
    [Parameter(Mandatory = $false)]
    [string] $Database,

    [Parameter(Mandatory = $false)]
    [string] $MigrationPath = './db/postgresql/migrations'
)

$ErrorActionPreference = 'Stop'

if ($null -eq (Get-Command psql -ErrorAction SilentlyContinue)) {
    throw 'psql is required and was not found on PATH.'
}

$root = (Resolve-Path $MigrationPath).Path
$migrations = @(Get-ChildItem -Path $root -Filter '*.sql' -File | Sort-Object Name)
if ($migrations.Count -eq 0) {
    throw "No PostgreSQL migrations were found in '$root'."
}

$scriptPath = Join-Path ([IO.Path]::GetTempPath()) "openportalkit-migrate-$([Guid]::NewGuid().ToString('N')).sql"
try {
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('\set ON_ERROR_STOP on')
    $lines.Add("select pg_advisory_lock(hashtext('openportalkit:migrations'));")
    $lines.Add(@'
create table if not exists opk_schema_migrations (
    migration_id text primary key,
    checksum text not null,
    applied_at timestamptz not null default now(),
    constraint ck_opk_schema_migration_checksum check (length(checksum) = 64)
);
'@)

    foreach ($migration in $migrations) {
        $id = $migration.BaseName.Replace("'", "''")
        $checksum = (Get-FileHash -Path $migration.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $path = $migration.FullName.Replace("'", "''")
        $lines.Add("\echo Checking $id")
        $lines.Add("select exists(select 1 from opk_schema_migrations where migration_id = '$id') as migration_applied \gset")
        $lines.Add("do `$check`$ begin if exists(select 1 from opk_schema_migrations where migration_id = '$id' and checksum <> '$checksum') then raise exception 'Checksum drift detected for migration $id.'; end if; end `$check`$;")
        $lines.Add('\if :migration_applied')
        $lines.Add("\echo Skipping applied migration $id")
        $lines.Add('\else')
        $lines.Add('begin;')
        $lines.Add("\ir '$path'")
        $lines.Add("insert into opk_schema_migrations (migration_id, checksum) values ('$id', '$checksum');")
        $lines.Add('commit;')
        $lines.Add('\endif')
    }

    $lines.Add("select pg_advisory_unlock(hashtext('openportalkit:migrations'));")
    $lines.Add('\echo OpenPortalKit migrations completed.')
    [IO.File]::WriteAllLines($scriptPath, $lines)

    $arguments = @('-X', '-v', 'ON_ERROR_STOP=1')
    if (-not [string]::IsNullOrWhiteSpace($Database)) {
        $arguments += @('-d', $Database)
    }
    $arguments += @('-f', $scriptPath)
    & psql @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "PostgreSQL migration failed with exit code $LASTEXITCODE."
    }
}
finally {
    if (Test-Path $scriptPath) {
        Remove-Item $scriptPath -Force
    }
}
