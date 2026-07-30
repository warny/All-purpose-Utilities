<#
.SYNOPSIS
Validates complete cross-platform candidate artifact assembly without network access.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content (Join-Path $PSScriptRoot "product-train-manifest.json") -Raw | ConvertFrom-Json
$testId = [guid]::NewGuid().ToString("N")
$relativeRoot = "artifacts/assembly-test-$testId"
$relativeOutput = "artifacts/assembly-output-$testId"
$root = Join-Path $repoRoot $relativeRoot
$output = Join-Path $repoRoot $relativeOutput
try {
    $acceptancePackages = @($manifest.packages | ForEach-Object { [ordered]@{ packageId = $_.packageId; restored = $true; compiled = $true } })
    foreach ($platform in @("ubuntu", "windows")) {
        New-Item (Join-Path $root "$platform/packages") -ItemType Directory -Force | Out-Null
        New-Item (Join-Path $root "$platform/reports") -ItemType Directory -Force | Out-Null
        [ordered]@{ version = [string]$manifest.version; platform = $platform; passed = $true; packages = $acceptancePackages } |
            ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root "$platform/reports/packaged-acceptance.json")
    }

    foreach ($package in $manifest.packages) {
        $nupkgName = "$($package.packageId).$($manifest.version).nupkg"
        $snupkgName = "$($package.packageId).$($manifest.version).snupkg"
        $ubuntuNupkg = Join-Path $root "ubuntu/packages/$nupkgName"
        $archive = [IO.Compression.ZipFile]::Open($ubuntuNupkg, [IO.Compression.ZipArchiveMode]::Create)
        $archive.Dispose()
        [IO.File]::WriteAllText((Join-Path $root "ubuntu/packages/$snupkgName"), "symbols-$($package.packageId)")
        Copy-Item $ubuntuNupkg (Join-Path $root "windows/packages/$nupkgName")
        Copy-Item (Join-Path $root "ubuntu/packages/$snupkgName") (Join-Path $root "windows/packages/$snupkgName")
    }

    $reports = Join-Path $root "ubuntu/reports"
    [ordered]@{ publicationOrder = @($manifest.packages.packageId); edges = @() } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $reports "package-graph.json")
    [ordered]@{} | ConvertTo-Json | Set-Content (Join-Path $reports "package-inspection.json")
    [ordered]@{ packages = @($manifest.packages | ForEach-Object { [ordered]@{ packageId = $_.packageId; result = "baseline-created" } }) } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $reports "public-api-comparison.json")
    [ordered]@{ packages = @($manifest.packages | ForEach-Object { [ordered]@{ packageId = $_.packageId; passed = $true } }) } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $reports "warnings.json")
    [ordered]@{ packages = @($manifest.packages | ForEach-Object { [ordered]@{ packageId = $_.packageId; passed = $true } }) } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $reports "sourcelink.json")

    New-Item (Join-Path $root "reproducibility/reports") -ItemType Directory -Force | Out-Null
    [ordered]@{ artifacts = @($manifest.packages | ForEach-Object { [ordered]@{ packageId = $_.packageId; result = "bit-identical" } }) } |
        ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root "reproducibility/reports/reproducibility-report.json")

    & (Join-Path $PSScriptRoot "assemble-validated-product-train.ps1") -InputsPath $relativeRoot -ArtifactsPath $relativeOutput
    $candidateManifest = Join-Path $output "manifests/release-candidate-manifest.json"
    if (-not (Test-Path -LiteralPath $candidateManifest -PathType Leaf)) { throw "Complete assembly did not produce the release-candidate manifest." }
    & (Join-Path $PSScriptRoot "publish-product-train.ps1") -ArtifactsPath $relativeOutput -ValidateCandidateOnly

    $firstPackage = $manifest.packages[0]
    [IO.File]::WriteAllText((Join-Path $root "windows/packages/$($firstPackage.packageId).$($manifest.version).nupkg"), "different-candidate")
    try {
        & (Join-Path $PSScriptRoot "assemble-validated-product-train.ps1") -InputsPath $relativeRoot -ValidateInputsOnly
        throw "Cross-platform package mismatch was not rejected."
    } catch {
        if ($_.Exception.Message -notmatch "differs between Ubuntu and Windows") { throw }
    }
} finally {
    Remove-Item $root -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $output -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "Release artifact assembly tests passed."
