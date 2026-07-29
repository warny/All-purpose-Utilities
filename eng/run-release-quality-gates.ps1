<#
.SYNOPSIS
Runs the complete, non-publishing repository release-quality pipeline.
#>
[CmdletBinding()]
param(
    [string] $Configuration = "Release",
    [string] $ArtifactsPath = "artifacts",
    [switch] $SkipRemoteSourceLink
)
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$originalPackages = $env:NUGET_PACKAGES
$isolated = Join-Path ([IO.Path]::GetFullPath((Join-Path $repoRoot $ArtifactsPath))) "quality-gate-packages"
try {
    $env:NUGET_PACKAGES = $isolated

    # PowerShell scripts propagate terminating errors directly. Do not inspect
    # LASTEXITCODE after these calls because it only describes the last native process.
    & (Join-Path $PSScriptRoot "test-release-orchestrator.ps1")

    & dotnet restore (Join-Path $repoRoot "Utils.sln")
    if ($LASTEXITCODE -ne 0) { throw "Solution restore failed." }
    & dotnet build (Join-Path $repoRoot "Utils.sln") --configuration $Configuration --no-restore -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw "Solution build failed." }
    & dotnet test (Join-Path $repoRoot "UtilsTest/UtilsTest.Unit.csproj") --configuration $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw "Unit tests failed." }
    & dotnet test (Join-Path $repoRoot "UtilsTest.Functional/UtilsTest.Functional.csproj") --configuration $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw "Functional tests failed." }

    foreach ($gate in @("discover-release-projects.ps1", "analyze-package-graph.ps1")) {
        & (Join-Path $PSScriptRoot $gate) -Configuration $Configuration -ArtifactsPath $ArtifactsPath
    }
    & (Join-Path $PSScriptRoot "test-packaged-product-train.ps1") -Configuration $Configuration -ArtifactsPath $ArtifactsPath -SkipBuild
    & (Join-Path $PSScriptRoot "validate-public-api.ps1") -Configuration $Configuration -ArtifactsPath $ArtifactsPath
    & (Join-Path $PSScriptRoot "validate-release-warnings.ps1") -Configuration $Configuration -ArtifactsPath $ArtifactsPath
    & (Join-Path $PSScriptRoot "validate-sourcelink.ps1") -ArtifactsPath $ArtifactsPath -SkipRemoteRetrieval:$SkipRemoteSourceLink
    & (Join-Path $PSScriptRoot "test-package-reproducibility.ps1") -Configuration $Configuration -ArtifactsPath $ArtifactsPath
    & (Join-Path $PSScriptRoot "audit-dependencies.ps1") -ArtifactsPath $ArtifactsPath
    & (Join-Path $PSScriptRoot "generate-release-candidate-manifest.ps1") -ArtifactsPath $ArtifactsPath
    Write-Host "Release quality gates passed. No package was published."
} finally {
    $env:NUGET_PACKAGES = $originalPackages
}
