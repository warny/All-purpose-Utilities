<#
.SYNOPSIS
Assembles the publication-compatible artifact from successful platform validations.
.DESCRIPTION
Requires Ubuntu and Windows acceptance reports plus the independent reproducibility
report, verifies that both platforms produced identical candidate package bytes, and
then generates the self-validating release-candidate manifest. No package is published.
#>
[CmdletBinding()]
param(
    [string] $InputsPath = "validated-inputs",
    [string] $ArtifactsPath = "artifacts",
    [switch] $ValidateInputsOnly
)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Release.Common.ps1")
$repoRoot = Split-Path -Parent $PSScriptRoot
$inputsRoot = Resolve-RepositoryPath $repoRoot $InputsPath
$artifactRoot = Resolve-RepositoryPath $repoRoot $ArtifactsPath
$ubuntuRoot = Join-Path $inputsRoot "ubuntu"
$windowsRoot = Join-Path $inputsRoot "windows"
$reproducibilityRoot = Join-Path $inputsRoot "reproducibility"

<# Returns and validates the packaged-acceptance report for one platform. #>
function Get-PassedAcceptanceReport {
    param([Parameter(Mandatory)][string] $Root, [Parameter(Mandatory)][string] $Platform)
    $path = Join-Path $Root "reports/packaged-acceptance.json"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "$Platform packaged-acceptance report is missing at '$path'." }
    $report = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    if (-not $report.passed -or @($report.packages | Where-Object { -not $_.restored -or -not $_.compiled })) {
        throw "$Platform packaged acceptance did not pass for every candidate package."
    }
    return $report
}

$ubuntuAcceptance = Get-PassedAcceptanceReport -Root $ubuntuRoot -Platform "Ubuntu"
$windowsAcceptance = Get-PassedAcceptanceReport -Root $windowsRoot -Platform "Windows"
if ($ubuntuAcceptance.version -ne $windowsAcceptance.version) { throw "Ubuntu and Windows validated different product-train versions." }

$ubuntuPackages = @(Get-ChildItem (Join-Path $ubuntuRoot "packages") -File | Where-Object Extension -in @(".nupkg", ".snupkg") | Sort-Object Name)
$windowsPackages = @(Get-ChildItem (Join-Path $windowsRoot "packages") -File | Where-Object Extension -in @(".nupkg", ".snupkg") | Sort-Object Name)
if (-not $ubuntuPackages -or (Compare-Object $ubuntuPackages.Name $windowsPackages.Name)) { throw "Ubuntu and Windows candidate package sets differ." }
$packageHashes = @()
foreach ($ubuntuPackage in $ubuntuPackages) {
    $windowsPackage = Join-Path (Join-Path $windowsRoot "packages") $ubuntuPackage.Name
    $ubuntuHash = (Get-FileHash $ubuntuPackage.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $windowsHash = (Get-FileHash $windowsPackage -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($ubuntuHash -ne $windowsHash) {
        throw "Candidate package '$($ubuntuPackage.Name)' differs between Ubuntu and Windows."
    }
    $packageHashes += [ordered]@{ file = $ubuntuPackage.Name; sha256 = $ubuntuHash }
}

$reproducibilityReport = Join-Path $reproducibilityRoot "reports/reproducibility-report.json"
if (-not (Test-Path -LiteralPath $reproducibilityReport -PathType Leaf)) { throw "Reproducibility report is missing at '$reproducibilityReport'." }
$reproducibility = Get-Content -LiteralPath $reproducibilityReport -Raw | ConvertFrom-Json
if (@($reproducibility.artifacts | Where-Object result -notin @("bit-identical", "logically-identical-after-zip-normalization"))) {
    throw "One or more candidate artifacts failed reproducibility validation."
}
if ($ValidateInputsOnly) {
    Write-Host "Validated Ubuntu, Windows, and reproducibility assembly inputs."
    return
}

Remove-Item -LiteralPath $artifactRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item (Join-Path $artifactRoot "packages") -ItemType Directory -Force | Out-Null
New-Item (Join-Path $artifactRoot "reports") -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $ubuntuRoot "packages/*") (Join-Path $artifactRoot "packages") -Force
Copy-Item (Join-Path $ubuntuRoot "reports/*") (Join-Path $artifactRoot "reports") -Recurse -Force
Copy-Item $reproducibilityReport (Join-Path $artifactRoot "reports/reproducibility-report.json") -Force
Write-ReleaseJson ([ordered]@{
    version = [string]$ubuntuAcceptance.version
    ubuntu = [ordered]@{ platform = [string]$ubuntuAcceptance.platform; passed = $true }
    windows = [ordered]@{ platform = [string]$windowsAcceptance.platform; passed = $true }
    byteIdenticalPackages = $packageHashes
}) (Join-Path $artifactRoot "reports/cross-platform-validation.json")

& (Join-Path $PSScriptRoot "generate-release-candidate-manifest.ps1") -ArtifactsPath $ArtifactsPath -RequireCrossPlatformValidation
Write-Host "Validated Ubuntu, Windows, and reproducibility results assembled at '$artifactRoot'."
