param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
$cliProject = Join-Path $Root "src/OpenPortalKit.Cli/OpenPortalKit.Cli.csproj"

if (-not (Test-Path -LiteralPath $cliProject)) {
    Write-Error "OpenPortalKit CLI project was not found at $cliProject."
    exit 2
}

& dotnet run --no-build --project $cliProject -- check-boundaries --root $Root
exit $LASTEXITCODE
