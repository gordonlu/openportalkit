param(
    [string]$Solution = "OpenPortalKit.sln"
)

$ErrorActionPreference = "Stop"
$json = & dotnet list $Solution package --vulnerable --include-transitive --format json
if ($LASTEXITCODE -ne 0) {
    throw "dotnet package vulnerability scan failed with exit code $LASTEXITCODE."
}

$report = $json | ConvertFrom-Json
$findings = @(
    foreach ($project in $report.projects) {
        foreach ($framework in $project.frameworks) {
            foreach ($package in @($framework.topLevelPackages) + @($framework.transitivePackages)) {
                foreach ($vulnerability in @($package.vulnerabilities)) {
                    [PSCustomObject]@{
                        Project = $project.path
                        Framework = $framework.framework
                        Package = $package.id
                        Version = $package.resolvedVersion
                        Severity = $vulnerability.severity
                        Advisory = $vulnerability.advisoryUrl
                    }
                }
            }
        }
    }
)

if ($findings.Count -gt 0) {
    $findings | Format-Table -AutoSize | Out-String | Write-Error
    throw "Dependency vulnerability scan found $($findings.Count) vulnerable package reference(s)."
}

Write-Host "No vulnerable .NET package references were reported."
