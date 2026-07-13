param(
    [string]$TestsRoot = "tests"
)

$ErrorActionPreference = "Stop"
$projects = @(Get-ChildItem -Path $TestsRoot -Recurse -File -Filter "*.Tests.csproj" | Sort-Object FullName)
if ($projects.Count -eq 0) {
    throw "No test projects were found under '$TestsRoot'."
}

$runCount = 0
$skippedCount = 0
foreach ($project in $projects) {
    $projectXml = Get-Content -Raw -LiteralPath $project.FullName
    if ($projectXml -match "<OpenPortalKitTestDependency>PostgreSQL</OpenPortalKitTestDependency>" -and
        [string]::IsNullOrWhiteSpace($env:OPK_POSTGRES_INTEGRATION)) {
        Write-Host "Skipping $($project.FullName): OPK_POSTGRES_INTEGRATION is not configured. This project is required by the PostgreSQL CI job."
        $skippedCount++
        continue
    }
    Write-Host "Running $($project.FullName)"
    & dotnet run --no-build --project $project.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "Test project '$($project.Name)' failed with exit code $LASTEXITCODE."
    }
    $runCount++
}

Write-Host "All $runCount configured test projects passed; $skippedCount external-dependency projects skipped."
