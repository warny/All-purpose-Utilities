<#
.SYNOPSIS
Validates SourceLink mappings, source checksums, and remote retrieval for every symbol package.
#>
[CmdletBinding()]
param([string] $ArtifactsPath = "artifacts", [switch] $SkipRemoteRetrieval, [TimeSpan] $ToolInstallTimeout = ([TimeSpan]::FromMinutes(5)), [TimeSpan] $RemoteTimeout = ([TimeSpan]::FromMinutes(3)))
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Release.Common.ps1")
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-ProductTrainManifest $repoRoot
$artifactRoot = Resolve-RepositoryPath $repoRoot $ArtifactsPath
$packageRoot = Join-Path $artifactRoot 'packages'; $workRoot = Join-Path $artifactRoot 'sourcelink'
Remove-Item $workRoot -Recurse -Force -ErrorAction SilentlyContinue; New-Item $workRoot -ItemType Directory -Force | Out-Null
$toolRoot = Join-Path $workRoot 'tool'
Invoke-NativeCommand -FilePath 'dotnet' -ArgumentList @('tool', 'install', 'sourcelink', '--tool-path', $toolRoot, '--version', '3.1.1') -Timeout $ToolInstallTimeout -LogPath (Join-Path $workRoot 'tool-install.log') | Out-Null
$tool = Join-Path $toolRoot $(if ($IsWindows) { 'sourcelink.exe' } else { 'sourcelink' })
$results = @()
foreach ($package in $manifest.packages) {
    $symbolPackage = Join-Path $packageRoot "$($package.packageId).$(Get-PackageVersion $manifest $package).snupkg"
    if (-not (Test-Path $symbolPackage)) { throw "$($package.packageId): symbol package is missing." }
    $extract = Join-Path $workRoot $package.packageId; Expand-ZipArchive $symbolPackage $extract
    $pdbs = @(Get-ChildItem $extract -Filter *.pdb -File -Recurse)
    if (-not $pdbs) { throw "$($package.packageId): no portable PDB was found." }
    $pdbResults = @()
    foreach ($pdb in $pdbs) {
        $jsonResult = Invoke-NativeCommand -FilePath $tool -ArgumentList @('print-json', $pdb.FullName) -Timeout ([TimeSpan]::FromMinutes(1)) -LogPath (Join-Path $workRoot "$($package.packageId)-$($pdb.Name)-json.log")
        $json = $jsonResult.StandardOutput
        if ($json -notmatch 'raw\.githubusercontent\.com|github\.com/.*/raw') { throw "$($package.packageId): invalid SourceLink mapping in '$($pdb.Name)'." }
        $urlResult = Invoke-NativeCommand -FilePath $tool -ArgumentList @('print-urls', $pdb.FullName) -Timeout ([TimeSpan]::FromMinutes(1)) -LogPath (Join-Path $workRoot "$($package.packageId)-$($pdb.Name)-urls.log")
        $urls = $urlResult.StandardOutput
        if ($urls -notmatch 'https://') { throw "$($package.packageId): documents are not remapped to remote URLs in '$($pdb.Name)'." }
        if (-not $SkipRemoteRetrieval) { Invoke-NativeCommand -FilePath $tool -ArgumentList @('test', $pdb.FullName) -Timeout $RemoteTimeout -LogPath (Join-Path $workRoot "$($package.packageId)-$($pdb.Name)-remote.log") | Out-Null }
        $pdbResults += [ordered]@{ pdb = $pdb.Name; mappings = 'valid'; checksums = if ($SkipRemoteRetrieval) { 'not-run' } else { 'valid' }; remoteRetrieval = if ($SkipRemoteRetrieval) { 'not-run' } else { 'passed' } }
    }
    $results += [ordered]@{ packageId = $package.packageId; pdbs = $pdbResults; passed = $true }
}
Write-ReleaseJson ([ordered]@{ commit = (& git -C $repoRoot rev-parse HEAD).Trim(); packages = $results }) (Join-Path $artifactRoot 'reports/sourcelink.json')
