param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$CommandArguments
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$cliProject = Join-Path $root "src/OpenPortalKit.Cli/OpenPortalKit.Cli.csproj"

& dotnet run --no-build --project $cliProject -- @CommandArguments
exit $LASTEXITCODE
