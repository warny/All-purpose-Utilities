<#
.SYNOPSIS
Validates the existing-package acceptance entry point without building or network access.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Release.Common.ps1")
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content (Join-Path $PSScriptRoot "product-train-manifest.json") -Raw | ConvertFrom-Json
$relativeArtifacts = "artifacts/existing-package-test-$([guid]::NewGuid().ToString('N'))"
$artifactRoot = Join-Path $repoRoot $relativeArtifacts
$packageRoot = Join-Path $artifactRoot "packages"
try {
    New-Item $packageRoot -ItemType Directory -Force | Out-Null
    foreach ($package in $manifest.packages) {
        $version = Get-PackageVersion $manifest $package
        foreach ($extension in @("nupkg", "snupkg")) {
            [IO.File]::WriteAllText((Join-Path $packageRoot "$($package.packageId).$version.$extension"), "$($package.packageId)-$extension")
        }
    }
    & (Join-Path $PSScriptRoot "write-canonical-package-report.ps1") -ArtifactsPath $relativeArtifacts
    $before = @(Get-ChildItem $packageRoot -File | ForEach-Object { [ordered]@{ name = $_.Name; hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash } })
    & (Join-Path $PSScriptRoot "test-packaged-product-train.ps1") -ArtifactsPath $relativeArtifacts -UseExistingPackages -ValidateExistingPackagesOnly
    $after = @(Get-ChildItem $packageRoot -File | ForEach-Object { [ordered]@{ name = $_.Name; hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash } })
    if (($before | ConvertTo-Json -Compress) -cne ($after | ConvertTo-Json -Compress)) { throw "Existing-package validation modified or rebuilt canonical packages." }
    if (Test-Path (Join-Path $artifactRoot "logs/packaged-acceptance")) { throw "Integrity-only existing-package validation unexpectedly invoked dotnet." }

    $first = $before[0]
    $firstPath = Join-Path $packageRoot $first.name
    $bytes = [IO.File]::ReadAllBytes($firstPath)
    Remove-Item $firstPath -Force
    try {
        & (Join-Path $PSScriptRoot "test-packaged-product-train.ps1") -ArtifactsPath $relativeArtifacts -UseExistingPackages -ValidateExistingPackagesOnly
        throw "A missing canonical package was not rejected."
    } catch {
        if ($_.Exception.Message -notmatch "does not contain the exact canonical package set") { throw }
    }
    [IO.File]::WriteAllBytes($firstPath, $bytes)
    [IO.File]::WriteAllText($firstPath, "altered-package")
    try {
        & (Join-Path $PSScriptRoot "test-packaged-product-train.ps1") -ArtifactsPath $relativeArtifacts -UseExistingPackages -ValidateExistingPackagesOnly
        throw "A canonical package hash mismatch was not rejected."
    } catch {
        if ($_.Exception.Message -notmatch "no longer matches canonical-packages.json") { throw }
    }
} finally {
    Remove-Item $artifactRoot -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "Existing canonical package validation tests passed."
