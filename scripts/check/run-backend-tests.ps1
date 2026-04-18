param(
    [string[]]$AdditionalArgs = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$solutionPath = Join-Path $repoRoot "backend\AiInterview.sln"

dotnet test $solutionPath --configuration Release --nologo @AdditionalArgs

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
