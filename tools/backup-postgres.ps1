param(
    [string] $Database,
    [string] $OutputDirectory = './backups'
)

$ErrorActionPreference = 'Stop'
if ($null -eq (Get-Command pg_dump -ErrorAction SilentlyContinue)) {
    throw 'pg_dump is required and was not found on PATH.'
}

$databaseName = if ([string]::IsNullOrWhiteSpace($Database)) { $env:PGDATABASE } else { $Database }
if ([string]::IsNullOrWhiteSpace($databaseName)) {
    throw 'Database must be provided with -Database or PGDATABASE.'
}

$resolvedOutputDirectory = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
} else {
    Join-Path (Get-Location) $OutputDirectory
}
$directory = [IO.Directory]::CreateDirectory($resolvedOutputDirectory).FullName
$timestamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$safeDatabaseName = $databaseName -replace '[^A-Za-z0-9_.-]', '_'
$finalPath = Join-Path $directory "openportalkit-$safeDatabaseName-$timestamp.dump"
$temporaryPath = "$finalPath.partial"
try {
    & pg_dump --format=custom --compress=6 --no-owner --no-privileges --file=$temporaryPath --dbname=$databaseName
    if ($LASTEXITCODE -ne 0) {
        throw "pg_dump failed with exit code $LASTEXITCODE."
    }
    if (-not (Test-Path $temporaryPath) -or (Get-Item $temporaryPath).Length -eq 0) {
        throw 'pg_dump did not create a non-empty backup.'
    }

    Move-Item $temporaryPath $finalPath
    $hash = (Get-FileHash -Path $finalPath -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText("$finalPath.sha256", "$hash  $([IO.Path]::GetFileName($finalPath))`n")
    [PSCustomObject]@{
        BackupPath = $finalPath
        ChecksumPath = "$finalPath.sha256"
        Sha256 = $hash
        SizeBytes = (Get-Item $finalPath).Length
        CreatedAt = [DateTimeOffset]::UtcNow
    } | ConvertTo-Json
}
finally {
    if (Test-Path $temporaryPath) {
        Remove-Item $temporaryPath -Force
    }
}
