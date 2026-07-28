[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()][string] $Configuration = "Release",
    [string] $ArtifactsPath = "artifacts"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $PSScriptRoot "parser-release-manifest.json"
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$packagePath = [IO.Path]::GetFullPath((Join-Path $repoRoot $ArtifactsPath))
$packagePath = Join-Path $packagePath "packages"

if (Test-Path $packagePath) { Remove-Item $packagePath -Recurse -Force }
New-Item $packagePath -ItemType Directory -Force | Out-Null

foreach ($package in $manifest.packages) {
    $project = Join-Path $repoRoot $package.project
    Write-Host "Pack: $($package.packageId) from $($package.project)"
    & dotnet pack $project --configuration $Configuration --no-build --no-restore --output $packagePath -p:ContinuousIntegrationBuild=true
    if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed for $($package.project)." }
}
