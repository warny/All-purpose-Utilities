<#
.SYNOPSIS
Validates SourceLink mappings, source checksums, and remote retrieval for every symbol package.
#>
[CmdletBinding()]
param([string] $ArtifactsPath = "artifacts", [switch] $SkipRemoteRetrieval)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Release.Common.ps1")
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-ProductTrainManifest $repoRoot
$artifactRoot = Resolve-RepositoryPath $repoRoot $ArtifactsPath
$packageRoot = Join-Path $artifactRoot 'packages'; $workRoot = Join-Path $artifactRoot 'sourcelink'
Remove-Item $workRoot -Recurse -Force -ErrorAction SilentlyContinue; New-Item $workRoot -ItemType Directory -Force | Out-Null
$toolRoot = Join-Path $workRoot 'tool'
& dotnet tool install sourcelink --tool-path $toolRoot --version 3.1.1
if ($LASTEXITCODE -ne 0) { throw 'SourceLink tool installation failed.' }
$tool = Join-Path $toolRoot $(if ($IsWindows) { 'sourcelink.exe' } else { 'sourcelink' })
$results = @()
foreach ($package in $manifest.packages) {
    $symbolPackage = Join-Path $packageRoot "$($package.packageId).$($manifest.version).snupkg"
    if (-not (Test-Path $symbolPackage)) { throw "$($package.packageId): symbol package is missing." }
    $extract = Join-Path $workRoot $package.packageId; Expand-Archive $symbolPackage $extract -Force
    $pdbs = @(Get-ChildItem $extract -Filter *.pdb -File -Recurse)
    if (-not $pdbs) { throw "$($package.packageId): no portable PDB was found." }
    $pdbResults = @()
    foreach ($pdb in $pdbs) {
        $json = & $tool print-json $pdb.FullName 2>&1
        if ($LASTEXITCODE -ne 0 -or ($json -join "`n") -notmatch 'raw\.githubusercontent\.com|github\.com/.*/raw') { throw "$($package.packageId): invalid SourceLink mapping in '$($pdb.Name)'." }
        $urls = & $tool print-urls $pdb.FullName 2>&1
        if ($LASTEXITCODE -ne 0 -or ($urls -join "`n") -notmatch 'https://') { throw "$($package.packageId): documents are not remapped to remote URLs in '$($pdb.Name)'." }
        if (-not $SkipRemoteRetrieval) { $test = & $tool test $pdb.FullName 2>&1; if ($LASTEXITCODE -ne 0) { $test | Write-Host; throw "$($package.packageId): remote source retrieval or checksum validation failed." } }
        $pdbResults += [ordered]@{ pdb = $pdb.Name; mappings = 'valid'; checksums = if ($SkipRemoteRetrieval) { 'not-run' } else { 'valid' }; remoteRetrieval = if ($SkipRemoteRetrieval) { 'not-run' } else { 'passed' } }
    }
    $results += [ordered]@{ packageId = $package.packageId; pdbs = $pdbResults; passed = $true }
}
Write-ReleaseJson ([ordered]@{ commit = (& git -C $repoRoot rev-parse HEAD).Trim(); packages = $results }) (Join-Path $artifactRoot 'reports/sourcelink.json')
