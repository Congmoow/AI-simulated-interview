param(
    [string[]]$AdditionalArgs = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$aiServicePath = Join-Path $repoRoot "ai-service"

uv run --directory $aiServicePath python -m pytest @AdditionalArgs

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
