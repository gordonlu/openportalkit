param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$corePaths = @(
    "src/OpenPortalKit.Kernel",
    "src/OpenPortalKit.Modules.Identity",
    "src/OpenPortalKit.Modules.Content",
    "src/OpenPortalKit.Modules.Assets",
    "src/OpenPortalKit.Modules.Workflow",
    "src/OpenPortalKit.Modules.Data",
    "src/OpenPortalKit.Modules.Search",
    "src/OpenPortalKit.Modules.Seo",
    "src/OpenPortalKit.Modules.AgentAccess",
    "src/OpenPortalKit.Modules.Dashboard",
    "src/OpenPortalKit.Modules.Audit",
    "src/OpenPortalKit.Modules.Jobs"
) | ForEach-Object {
    Join-Path $Root $_
}

$forbidden = @(
    "Fund",
    "IPO",
    "Stock",
    "Security",
    "Broker",
    "Finance",
    "MarketCommentary",
    "RiskDisclosure",
    "FundNav",
    "IpoProject"
)

$files = foreach ($path in $corePaths) {
    if (Test-Path $path) {
        Get-ChildItem -Path $path -Recurse -File -Include *.cs,*.csproj
    }
}

if (-not $files) {
    Write-Error "Core boundary check failed: no core source files were found."
    exit 1
}

$violations = foreach ($file in $files) {
    $content = Get-Content -Raw -LiteralPath $file.FullName
    foreach ($term in $forbidden) {
        if ($content -match "\b$([Regex]::Escape($term))\b") {
            [PSCustomObject]@{
                File = $file.FullName.Substring($Root.Length + 1)
                Term = $term
            }
        }
    }
}

if ($violations) {
    $violations | Format-Table -AutoSize
    Write-Error "Core boundary check failed: forbidden industry-specific terms found in core source."
    exit 1
}

Write-Host "Core boundary check passed."
