<#
.SYNOPSIS
Records and verifies the immutable package files produced by the canonical packaging job.
#>
[CmdletBinding()]
param([string] $ArtifactsPath = "artifacts")
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Release.Common.ps1")
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-ProductTrainManifest $repoRoot
$artifactRoot = Resolve-RepositoryPath $repoRoot $ArtifactsPath
$packageRoot = Join-Path $artifactRoot "packages"
$expectedFiles = @($manifest.packages | ForEach-Object {
    "$($_.packageId).$($manifest.version).nupkg"
    if ($_.symbolPackage) { "$($_.packageId).$($manifest.version).snupkg" }
} | Sort-Object)
$actualFiles = @(Get-ChildItem $packageRoot -File | Where-Object Extension -in @(".nupkg", ".snupkg") | Select-Object -ExpandProperty Name | Sort-Object)
$difference = Compare-Object $expectedFiles $actualFiles
if ($difference) {
    throw "Canonical package set differs from the product-train manifest: $($difference.InputObject -join ', ')."
}
$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw "Unable to resolve the canonical package commit." }
$packages = @($actualFiles | ForEach-Object {
    $path = Join-Path $packageRoot $_
    [ordered]@{ file = $_; sha256 = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant() }
})
Write-ReleaseJson ([ordered]@{
    productTrain = [string]$manifest.productTrain
    commit = $commit
    version = [string]$manifest.version
    canonicalPlatform = "ubuntu"
    packages = $packages
}) (Join-Path $artifactRoot "reports/canonical-packages.json")
Write-Host "Recorded $($packages.Count) immutable canonical package files."
