<#
.SYNOPSIS
Assembles the publication-compatible artifact from canonical packages and platform validations.
.DESCRIPTION
Requires one immutable package set, Ubuntu and Windows acceptance reports for that exact
set, and an independent reproducibility report. No package is built or published.
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
$manifest = Get-ProductTrainManifest $repoRoot
$inputsRoot = Resolve-RepositoryPath $repoRoot $InputsPath
$artifactRoot = Resolve-RepositoryPath $repoRoot $ArtifactsPath
$canonicalRoot = Join-Path $inputsRoot "canonical"
$reproducibilityRoot = Join-Path $inputsRoot "reproducibility"
$canonicalReportPath = Join-Path $canonicalRoot "reports/canonical-packages.json"
if (-not (Test-Path -LiteralPath $canonicalReportPath -PathType Leaf)) {
    throw "Canonical package report is missing at '$canonicalReportPath'."
}
$canonical = Get-Content -LiteralPath $canonicalReportPath -Raw | ConvertFrom-Json
$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw "Unable to resolve the assembly commit." }
if ($canonical.productTrain -ne $manifest.productTrain -or $canonical.version -ne $manifest.version -or $canonical.commit -ne $commit) {
    throw "Canonical package report identity, version, or commit does not match the assembly commit."
}
$expectedFiles = @($manifest.packages | ForEach-Object {
    "$($_.packageId).$($manifest.version).nupkg"
    if ($_.symbolPackage) { "$($_.packageId).$($manifest.version).snupkg" }
} | Sort-Object)
$canonicalFiles = @($canonical.packages.file | Sort-Object)
if (Compare-Object $expectedFiles $canonicalFiles) { throw "Canonical report does not contain the exact product-train package set." }
foreach ($item in $canonical.packages) {
    $path = Join-Path $canonicalRoot "packages/$($item.file)"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Canonical package '$($item.file)' is missing." }
    $actualHash = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $item.sha256) {
        throw "Canonical package '$($item.file)' no longer matches canonical-packages.json."
    }
}

<# Returns a platform report after proving that it validated every canonical artifact. #>
function Get-CanonicalAcceptanceReport {
    param([Parameter(Mandatory)][string] $Platform)
    $root = Join-Path $inputsRoot $Platform.ToLowerInvariant()
    $path = Join-Path $root "reports/packaged-acceptance.json"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "$Platform packaged-acceptance report is missing at '$path'." }
    $report = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    if (-not $report.passed) { throw "$Platform packaged acceptance did not pass." }
    if ($report.productTrain -ne $canonical.productTrain -or $report.version -ne $canonical.version -or $report.commit -ne $canonical.commit) {
        throw "$Platform validation identity, version, or commit differs from the canonical package report."
    }
    foreach ($definition in $manifest.packages) {
        $validatedPackage = @($report.packages | Where-Object packageId -eq $definition.packageId)
        if ($validatedPackage.Count -ne 1 -or -not $validatedPackage[0].restored -or -not $validatedPackage[0].compiled) {
            throw "$Platform validation is missing package '$($definition.packageId)'."
        }
    }
    foreach ($item in $canonical.packages) {
        $validated = @($report.artifacts | Where-Object file -eq $item.file)
        if ($validated.Count -ne 1) { throw "$Platform validation is missing package '$($item.file)'." }
        if ($validated[0].sha256 -ne $item.sha256) {
            throw "$Platform validation did not use canonical package '$($item.file)': expected SHA-256 $($item.sha256), reported $($validated[0].sha256)."
        }
    }
    if (@($report.artifacts).Count -ne @($canonical.packages).Count) {
        throw "$Platform validation reported unexpected canonical package files."
    }
    return $report
}

$ubuntuAcceptance = Get-CanonicalAcceptanceReport -Platform "Ubuntu"
$windowsAcceptance = Get-CanonicalAcceptanceReport -Platform "Windows"
$reproducibilityReport = Join-Path $reproducibilityRoot "reports/reproducibility-report.json"
if (-not (Test-Path -LiteralPath $reproducibilityReport -PathType Leaf)) { throw "Reproducibility report is missing at '$reproducibilityReport'." }
$reproducibility = Get-Content -LiteralPath $reproducibilityReport -Raw | ConvertFrom-Json
if (@($reproducibility.artifacts | Where-Object result -notin @("bit-identical", "logically-identical-after-zip-normalization"))) {
    throw "One or more candidate artifacts failed reproducibility validation."
}
if ($ValidateInputsOnly) {
    Write-Host "Validated canonical packages, Ubuntu, Windows, and reproducibility assembly inputs."
    return
}

Remove-Item -LiteralPath $artifactRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item (Join-Path $artifactRoot "packages") -ItemType Directory -Force | Out-Null
New-Item (Join-Path $artifactRoot "reports") -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $canonicalRoot "packages/*") (Join-Path $artifactRoot "packages") -Force
Copy-Item (Join-Path $canonicalRoot "reports/*") (Join-Path $artifactRoot "reports") -Recurse -Force
Copy-Item (Join-Path $inputsRoot "ubuntu/reports/*") (Join-Path $artifactRoot "reports") -Recurse -Force
Copy-Item $reproducibilityReport (Join-Path $artifactRoot "reports/reproducibility-report.json") -Force
Write-ReleaseJson ([ordered]@{
    productTrain = [string]$canonical.productTrain
    commit = [string]$canonical.commit
    version = [string]$canonical.version
    canonicalPlatform = [string]$canonical.canonicalPlatform
    ubuntu = [ordered]@{ platform = [string]$ubuntuAcceptance.platform; passed = $true }
    windows = [ordered]@{ platform = [string]$windowsAcceptance.platform; passed = $true }
    packages = @($canonical.packages | ForEach-Object {
        [ordered]@{ file = $_.file; sha256 = $_.sha256; validatedOn = @("ubuntu", "windows") }
    })
}) (Join-Path $artifactRoot "reports/cross-platform-validation.json")

& (Join-Path $PSScriptRoot "generate-release-candidate-manifest.ps1") -ArtifactsPath $ArtifactsPath -RequireCrossPlatformValidation
& (Join-Path $PSScriptRoot "publish-product-train.ps1") -ArtifactsPath $ArtifactsPath -ValidateCandidateOnly
Write-Host "Canonical packages and cross-platform validation results assembled at '$artifactRoot'."
