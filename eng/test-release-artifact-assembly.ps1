<#
.SYNOPSIS
Validates complete canonical package assembly and failure diagnostics without network access.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem
. (Join-Path $PSScriptRoot "Release.Common.ps1")
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content (Join-Path $PSScriptRoot "product-train-manifest.json") -Raw | ConvertFrom-Json
$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
$testId = [guid]::NewGuid().ToString("N")
$relativeRoot = "artifacts/assembly-test-$testId"
$relativeOutput = "artifacts/assembly-output-$testId"
$root = Join-Path $repoRoot $relativeRoot
$output = Join-Path $repoRoot $relativeOutput

<# Writes a JSON fixture using the same stable formatting as release scripts. #>
function Write-FixtureJson {
    param([Parameter(Mandatory)] $Value, [Parameter(Mandatory)][string] $Path)
    New-Item (Split-Path -Parent $Path) -ItemType Directory -Force | Out-Null
    $Value | ConvertTo-Json -Depth 10 | Set-Content $Path
}

try {
    $canonicalPackages = @()
    New-Item (Join-Path $root "canonical/packages") -ItemType Directory -Force | Out-Null
    foreach ($package in $manifest.packages) {
        $packageVersion = [string]$manifest.version
        foreach ($extension in @("nupkg", "snupkg")) {
            $name = "$($package.packageId).$packageVersion.$extension"
            $path = Join-Path $root "canonical/packages/$name"
            $archive = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
            try {
                $entry = $archive.CreateEntry("content/$($package.packageId).txt")
                $writer = [IO.StreamWriter]::new($entry.Open())
                try { $writer.Write("candidate-$($package.packageId)-$extension") } finally { $writer.Dispose() }
            } finally {
                $archive.Dispose()
            }
            $canonicalPackages += [ordered]@{ file = $name; sha256 = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant() }
        }
    }
    $canonicalReport = [ordered]@{ productTrain = [string]$manifest.productTrain; commit = $commit; version = [string]$manifest.version; canonicalPlatform = "ubuntu"; packages = $canonicalPackages }
    Write-FixtureJson $canonicalReport (Join-Path $root "canonical/reports/canonical-packages.json")
    Write-FixtureJson ([ordered]@{ publicationOrder = @($manifest.packages.packageId); edges = @() }) (Join-Path $root "canonical/reports/package-graph.json")
    Write-FixtureJson ([ordered]@{}) (Join-Path $root "canonical/reports/package-inspection.json")

    $acceptancePackages = @($manifest.packages | ForEach-Object { [ordered]@{ packageId = $_.packageId; restored = $true; compiled = $true } })
    foreach ($platform in @("ubuntu", "windows")) {
        $acceptance = [ordered]@{
            productTrain = [string]$manifest.productTrain
            commit = $commit
            version = [string]$manifest.version
            platform = $platform
            passed = $true
            artifacts = @($canonicalPackages | ForEach-Object { [ordered]@{ file = $_.file; sha256 = $_.sha256 } })
            packages = $acceptancePackages
        }
        Write-FixtureJson $acceptance (Join-Path $root "$platform/reports/packaged-acceptance.json")
    }

    $reports = Join-Path $root "ubuntu/reports"
    Write-FixtureJson ([ordered]@{ packages = @($manifest.packages | ForEach-Object { [ordered]@{ packageId = $_.packageId; result = "baseline-created" } }) }) (Join-Path $reports "public-api-comparison.json")
    Write-FixtureJson ([ordered]@{ packages = @($manifest.packages | ForEach-Object { [ordered]@{ packageId = $_.packageId; passed = $true } }) }) (Join-Path $reports "warnings.json")
    Write-FixtureJson ([ordered]@{ packages = @($manifest.packages | ForEach-Object { [ordered]@{ packageId = $_.packageId; passed = $true } }) }) (Join-Path $reports "sourcelink.json")
    Write-FixtureJson ([ordered]@{ artifacts = @($manifest.packages | ForEach-Object { [ordered]@{ packageId = $_.packageId; result = "bit-identical" } }) }) (Join-Path $root "reproducibility/reports/reproducibility-report.json")

    & (Join-Path $PSScriptRoot "assemble-validated-product-train.ps1") -InputsPath $relativeRoot -ArtifactsPath $relativeOutput
    $candidateManifest = Join-Path $output "manifests/release-candidate-manifest.json"
    if (-not (Test-Path -LiteralPath $candidateManifest -PathType Leaf)) { throw "Complete assembly did not produce the release-candidate manifest." }
    & (Join-Path $PSScriptRoot "publish-product-train.ps1") -ArtifactsPath $relativeOutput -ValidateCandidateOnly

    # omy.Utils.Collections is an independent provisional package (see
    # docs/releasing/ProvisionalVersioning.md) and must never re-enter the train's canonical
    # package set, candidate manifest, or publication order - even though it already ships its
    # own 0.0.1 nupkg independently. No network access is needed: the manifest itself, and the
    # candidate manifest derived from it, are asserted directly.
    if ($manifest.packages.packageId -contains 'omy.Utils.Collections') {
        throw "omy.Utils.Collections must not be listed in the product train's packages[] array."
    }
    $collectionsExclusion = @($manifest.exclusions | Where-Object project -eq 'Utils.Collections/Utils.Collections.csproj')
    if ($collectionsExclusion.Count -ne 1 -or $collectionsExclusion[0].classification -ne 'provisional-package') {
        throw "omy.Utils.Collections must have exactly one exclusions[] entry classified 'provisional-package'."
    }
    $candidateJson = Get-Content $candidateManifest -Raw | ConvertFrom-Json
    if ($candidateJson.packages.packageId -contains 'omy.Utils.Collections' -or $candidateJson.publicationOrder -contains 'omy.Utils.Collections') {
        throw "The release-candidate manifest must not reference omy.Utils.Collections."
    }
    # publish-product-train.ps1's remote all-or-none preflight (and -PreflightPackageIdsOnly)
    # only ever iterates $manifest.packages, so the assertion above that omy.Utils.Collections is
    # absent from that array is sufficient proof it can never enter the preflight's expected-ID
    # or remote-state computation, regardless of whether omy.Utils.Collections 0.0.1 already
    # exists independently on NuGet.

    # Candidate-only inspection accepts PR fixtures, but publication-capable modes
    # reject them before contacting NuGet.
    $fullCandidate = Get-Content $candidateManifest -Raw
    $pullRequestCandidate = $fullCandidate | ConvertFrom-Json
    $pullRequestCandidate.validationTier = 'pull-request'
    $pullRequestCandidate.reproducibilityValidated = $false
    foreach ($package in $pullRequestCandidate.packages) { $package.reproducibilityResult = @() }
    Write-FixtureJson $pullRequestCandidate $candidateManifest
    & (Join-Path $PSScriptRoot "publish-product-train.ps1") -ArtifactsPath $relativeOutput -ValidateCandidateOnly
    try {
        & (Join-Path $PSScriptRoot "publish-product-train.ps1") -ArtifactsPath $relativeOutput
        throw 'A pull-request candidate reached publication planning.'
    } catch {
        if ($_.Exception.Message -notmatch 'Publication requires a full-release candidate') { throw }
    }
    Set-Content $candidateManifest $fullCandidate

    $windowsReportPath = Join-Path $root "windows/reports/packaged-acceptance.json"
    $windowsReport = Get-Content $windowsReportPath -Raw | ConvertFrom-Json
    $windowsReport.artifacts[0].sha256 = "incorrect-windows-hash"
    Write-FixtureJson $windowsReport $windowsReportPath
    try {
        & (Join-Path $PSScriptRoot "assemble-validated-product-train.ps1") -InputsPath $relativeRoot -ValidateInputsOnly
        throw "A divergent Windows validation hash was not rejected."
    } catch {
        if ($_.Exception.Message -notmatch "Windows validation did not use canonical package") { throw }
    }
    $windowsReport.artifacts[0].sha256 = $canonicalPackages[0].sha256
    Write-FixtureJson $windowsReport $windowsReportPath

    $canonicalPath = Join-Path $root "canonical/packages/$($canonicalPackages[0].file)"
    $originalBytes = [IO.File]::ReadAllBytes($canonicalPath)
    try {
        [IO.File]::WriteAllBytes($canonicalPath, @($originalBytes + [byte]0))
        try {
            & (Join-Path $PSScriptRoot "assemble-validated-product-train.ps1") -InputsPath $relativeRoot -ValidateInputsOnly
            throw "An altered canonical package was not rejected."
        } catch {
            if ($_.Exception.Message -notmatch "no longer matches canonical-packages.json") { throw }
        }
    } finally {
        [IO.File]::WriteAllBytes($canonicalPath, $originalBytes)
    }

    $windowsReport = Get-Content $windowsReportPath -Raw | ConvertFrom-Json
    $missingFile = $windowsReport.artifacts[0].file
    $windowsReport.artifacts = @($windowsReport.artifacts | Select-Object -Skip 1)
    Write-FixtureJson $windowsReport $windowsReportPath
    try {
        & (Join-Path $PSScriptRoot "assemble-validated-product-train.ps1") -InputsPath $relativeRoot -ValidateInputsOnly
        throw "An incomplete Windows validation package list was not rejected."
    } catch {
        if ($_.Exception.Message -notmatch "Windows validation is missing package '$([regex]::Escape($missingFile))'") { throw }
    }
} finally {
    Remove-Item $root -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $output -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "Release artifact assembly tests passed."
