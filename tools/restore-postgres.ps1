param(
    [Parameter(Mandatory = $true)]
    [string] $BackupPath,
    [string] $Database,
    [switch] $Apply
)

$ErrorActionPreference = 'Stop'
if ($null -eq (Get-Command pg_restore -ErrorAction SilentlyContinue)) {
    throw 'pg_restore is required and was not found on PATH.'
}

$resolvedBackup = (Resolve-Path $BackupPath).Path
$checksumPath = "$resolvedBackup.sha256"
if (-not (Test-Path $checksumPath)) {
    throw "Backup checksum file '$checksumPath' is missing."
}

$expectedHash = ((Get-Content $checksumPath -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
$actualHash = (Get-FileHash -Path $resolvedBackup -Algorithm SHA256).Hash.ToLowerInvariant()
if ($expectedHash -ne $actualHash) {
    throw 'Backup checksum validation failed.'
}

& pg_restore --list $resolvedBackup | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "pg_restore could not read the backup archive (exit code $LASTEXITCODE)."
}

if (-not $Apply) {
    [PSCustomObject]@{ BackupPath = $resolvedBackup; Sha256 = $actualHash; ArchiveValid = $true; Applied = $false } |
        ConvertTo-Json
    return
}

$databaseName = if ([string]::IsNullOrWhiteSpace($Database)) { $env:PGDATABASE } else { $Database }
if ([string]::IsNullOrWhiteSpace($databaseName)) {
    throw 'Database must be provided with -Database or PGDATABASE when -Apply is used.'
}

& pg_restore --exit-on-error --single-transaction --clean --if-exists --no-owner --no-privileges --dbname=$databaseName $resolvedBackup
if ($LASTEXITCODE -ne 0) {
    throw "pg_restore failed with exit code $LASTEXITCODE."
}

[PSCustomObject]@{ BackupPath = $resolvedBackup; Sha256 = $actualHash; ArchiveValid = $true; Applied = $true; Database = $databaseName } |
    ConvertTo-Json
