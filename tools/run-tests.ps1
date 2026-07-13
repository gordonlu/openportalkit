param(
    [string]$TestsRoot = "tests"
)

$ErrorActionPreference = "Stop"
$projects = @(Get-ChildItem -Path $TestsRoot -Recurse -File -Filter "*.Tests.csproj" | Sort-Object FullName)
if ($projects.Count -eq 0) {
    throw "No test projects were found under '$TestsRoot'."
}

foreach ($project in $projects) {
    Write-Host "Running $($project.FullName)"
    & dotnet run --no-build --project $project.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "Test project '$($project.Name)' failed with exit code $LASTEXITCODE."
    }
}

Write-Host "All $($projects.Count) test projects passed."
