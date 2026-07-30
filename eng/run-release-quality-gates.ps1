<#
.SYNOPSIS
Runs observable pull-request or complete release-quality validation.
.DESCRIPTION
PullRequest mode avoids repeating build and test jobs and omits remote or advisory
checks. FullRelease remains the authoritative non-publishing pre-release pipeline.
#>
[CmdletBinding()]
param(
    [ValidateSet("PullRequest", "FullRelease")][string] $Mode = "FullRelease",
    [string] $Configuration = "Release",
    [string] $ArtifactsPath = "artifacts",
    [switch] $SkipBuild,
    [switch] $SkipTests,
    [switch] $SkipPackagedAcceptance,
    [switch] $SkipReproducibility,
    [switch] $SkipRemoteSourceLink,
    [switch] $SkipOutdatedDependencyAudit,
    [switch] $PlanOnly
)
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "Release.Common.ps1")
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $ArtifactsPath))
$logRoot = Join-Path $artifactRoot "logs"
$originalPackages = $env:NUGET_PACKAGES
$env:NUGET_PACKAGES = Join-Path $artifactRoot "quality-gate-packages"

if ($Mode -eq "PullRequest") {
    $SkipBuild = $true
    $SkipTests = $true
    $SkipReproducibility = $true
    $SkipRemoteSourceLink = $true
    $SkipOutdatedDependencyAudit = $true
}

$gates = @(
    [pscustomobject]@{ Name = "build"; Display = "Restore and build"; Skip = [bool]$SkipBuild; Action = {
        Invoke-NativeCommand dotnet @("restore", (Join-Path $repoRoot "Utils.sln")) ([TimeSpan]::FromMinutes(15)) (Join-Path $logRoot "restore.log") | Out-Null
        Invoke-NativeCommand dotnet @("build", (Join-Path $repoRoot "Utils.sln"), "--configuration", $Configuration, "--no-restore", "-p:UseSharedCompilation=false") ([TimeSpan]::FromMinutes(20)) (Join-Path $logRoot "build.log") | Out-Null
    } },
    [pscustomobject]@{ Name = "tests"; Display = "Unit and functional tests"; Skip = [bool]$SkipTests; Action = {
        Invoke-NativeCommand dotnet @("test", (Join-Path $repoRoot "UtilsTest/UtilsTest.Unit.csproj"), "--configuration", $Configuration, "--no-build") ([TimeSpan]::FromMinutes(15)) (Join-Path $logRoot "unit-tests.log") | Out-Null
        Invoke-NativeCommand dotnet @("test", (Join-Path $repoRoot "UtilsTest.Functional/UtilsTest.Functional.csproj"), "--configuration", $Configuration, "--no-build") ([TimeSpan]::FromMinutes(20)) (Join-Path $logRoot "functional-tests.log") | Out-Null
    } },
    [pscustomobject]@{ Name = "package-discovery"; Display = "Package discovery and graph"; Skip = $false; Action = { & "$PSScriptRoot/discover-release-projects.ps1" -Configuration $Configuration -ArtifactsPath $ArtifactsPath; & "$PSScriptRoot/analyze-package-graph.ps1" -Configuration $Configuration -ArtifactsPath $ArtifactsPath } },
    [pscustomobject]@{ Name = "packaged-product-train"; Display = "Packaged product train"; Skip = [bool]$SkipPackagedAcceptance; Action = { & "$PSScriptRoot/test-packaged-product-train.ps1" -Configuration $Configuration -ArtifactsPath $ArtifactsPath -SkipBuild } },
    [pscustomobject]@{ Name = "api-compatibility"; Display = "API compatibility"; Skip = $false; Action = { & "$PSScriptRoot/validate-public-api.ps1" -Configuration $Configuration -ArtifactsPath $ArtifactsPath } },
    [pscustomobject]@{ Name = "release-warnings"; Display = "Release warnings"; Skip = $false; Action = { & "$PSScriptRoot/validate-release-warnings.ps1" -Configuration $Configuration -ArtifactsPath $ArtifactsPath -NoRestore } },
    [pscustomobject]@{ Name = "sourcelink"; Display = "SourceLink validation"; Skip = $false; Action = { & "$PSScriptRoot/validate-sourcelink.ps1" -ArtifactsPath $ArtifactsPath -SkipRemoteRetrieval:$SkipRemoteSourceLink } },
    [pscustomobject]@{ Name = "reproducibility"; Display = "Package reproducibility"; Skip = [bool]$SkipReproducibility; Action = { & "$PSScriptRoot/test-package-reproducibility.ps1" -Configuration $Configuration -ArtifactsPath $ArtifactsPath } },
    [pscustomobject]@{ Name = "dependency-audit"; Display = "Dependency audit"; Skip = $false; Action = { & "$PSScriptRoot/audit-dependencies.ps1" -ArtifactsPath $ArtifactsPath -SkipOutdated:$SkipOutdatedDependencyAudit } },
    [pscustomobject]@{ Name = "release-manifest"; Display = "Release candidate manifest"; Skip = $false; Action = { & "$PSScriptRoot/generate-release-candidate-manifest.ps1" -ArtifactsPath $ArtifactsPath } }
)

try {
    foreach ($gate in $gates) {
        if ($gate.Skip) { Write-Host "SKIP $($gate.Name)"; continue }
        if ($PlanOnly) { Write-Host "RUN $($gate.Name)"; continue }
        Invoke-ReleaseGate -Name $gate.Name -DisplayName $gate.Display -Action $gate.Action
    }
    if (-not $PlanOnly) { Write-Host "Release quality gates passed in $Mode mode. No package was published." }
} finally {
    $env:NUGET_PACKAGES = $originalPackages
}
