<#
.SYNOPSIS
Packs, inspects, and functionally exercises every manifest-excluded "provisional-package" NuGet
package (currently omy.Utils.Collections) without contacting NuGet.
.DESCRIPTION
These packages are deliberately outside eng/product-train-manifest.json's packages array (see
docs/releasing/ProvisionalVersioning.md) and therefore skip every train-wide gate: canonical
packaging, inspect-packages.ps1, validate-sourcelink.ps1, packaged-consumer acceptance, API
compatibility, and reproducibility. This script is a much lighter, package-scoped substitute so a
provisional package's first publish is not entirely unverified: it packs the real project, inspects
the resulting archives (version, required files, icon), validates SourceLink, and restores/builds/
runs a minimal real consumer against the packed .nupkg through an isolated local feed.
#>
[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()][string] $Configuration = "Release",
    [string] $ArtifactsPath = "artifacts/provisional-package-test",
    [switch] $SkipRemoteSourceLinkRetrieval
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem
. (Join-Path $PSScriptRoot "Release.Common.ps1")
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-ProductTrainManifest $repoRoot
$artifactRoot = Resolve-RepositoryPath $repoRoot $ArtifactsPath
Remove-Item $artifactRoot -Recurse -Force -ErrorAction SilentlyContinue
$packageRoot = Join-Path $artifactRoot "packages"
$workRoot = Join-Path $artifactRoot "work"
New-Item $packageRoot -ItemType Directory -Force | Out-Null
New-Item $workRoot -ItemType Directory -Force | Out-Null

$provisionalPackages = @($manifest.exclusions | Where-Object classification -eq 'provisional-package')
if (-not $provisionalPackages) {
    Write-Host "No 'provisional-package' entries in the manifest's exclusions[]; nothing to validate."
    return
}
if ($manifest.packages.project | Where-Object { $provisionalPackages.project -contains $_ }) {
    throw "A provisional-package exclusion also appears in packages[] - it can no longer be both."
}

foreach ($entry in $provisionalPackages) {
    $projectPath = Resolve-RepositoryPath $repoRoot $entry.project
    [xml]$xml = Get-Content $projectPath -Raw
    $versionNodes = @($xml.SelectNodes('/Project/PropertyGroup/Version'))
    if ($versionNodes.Count -ne 1 -or [string]::IsNullOrWhiteSpace($versionNodes[0].InnerText)) {
        throw "$($entry.project): expected exactly one literal <Version> (this package must not use `$(ProductTrainVersion))."
    }
    $version = $versionNodes[0].InnerText.Trim()
    if ($version -eq '$(ProductTrainVersion)') {
        throw "$($entry.project): declares `$(ProductTrainVersion) - a provisional-package exclusion must have its own literal version."
    }
    $packageId = [string]$entry.packageId
    Write-Host "Provisional package: $packageId $version ($($entry.project))"

    $packLog = Join-Path $artifactRoot "logs/pack-$packageId.log"
    $dotnetPath = @(Get-Command dotnet -CommandType Application)[0].Source
    Invoke-NativeCommand -FilePath $dotnetPath -ArgumentList @("pack", $projectPath, "--configuration", $Configuration, "--output", $packageRoot, "-p:ContinuousIntegrationBuild=true") -Timeout ([TimeSpan]::FromMinutes(10)) -LogPath $packLog | Out-Null

    $nupkgPath = Join-Path $packageRoot "$packageId.$version.nupkg"
    $snupkgPath = Join-Path $packageRoot "$packageId.$version.snupkg"
    if (-not (Test-Path -LiteralPath $nupkgPath)) { throw "$packageId): expected package '$nupkgPath' is missing." }
    if (-not (Test-Path -LiteralPath $snupkgPath)) { throw "${packageId}: expected symbol package '$snupkgPath' is missing." }

    $archive = [IO.Compression.ZipFile]::OpenRead($nupkgPath)
    try {
        $entries = @($archive.Entries | ForEach-Object FullName)
        $nuspecEntry = @($archive.Entries | Where-Object FullName -like '*.nuspec')
        if ($nuspecEntry.Count -ne 1) { throw "${packageId}: expected exactly one nuspec." }
        $reader = [IO.StreamReader]::new($nuspecEntry[0].Open()); try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $metadata = $nuspec.package.metadata
        if ([string]$metadata.id -ne $packageId) { throw "${packageId}: nuspec id '$([string]$metadata.id)' does not match the manifest entry." }
        if ([string]$metadata.version -ne $version) { throw "${packageId}: nuspec version '$([string]$metadata.version)' does not match the project's literal <Version> '$version'." }
        foreach ($required in @('README.md', 'LICENSE-apache-2.0.txt', 'AllPurposeUtilities_logo.png')) {
            if ($entries -notcontains $required) { throw "${packageId}: '$required' is missing from the package." }
        }
        if ([string]$metadata.icon -ne 'AllPurposeUtilities_logo.png') { throw "${packageId}: nuspec <icon> is '$([string]$metadata.icon)', expected 'AllPurposeUtilities_logo.png'." }
        $licenseText = if ($metadata.license -is [Xml.XmlElement]) { $metadata.license.InnerText } else { [string]$metadata.license }
        if ([string]::IsNullOrWhiteSpace($licenseText)) { throw "${packageId}: nuspec is missing a <license> declaration." }
        Write-Host "  nupkg: id/version/README/license/logo verified."
    } finally { $archive.Dispose() }

    # SourceLink: the symbol package's portable PDB must map to remote, retrievable source.
    $toolRoot = Join-Path $workRoot "sourcelink-tool"
    if (-not (Test-Path $toolRoot)) {
        Invoke-NativeCommand -FilePath $dotnetPath -ArgumentList @('tool', 'install', 'sourcelink', '--tool-path', $toolRoot, '--version', '3.1.1') -Timeout ([TimeSpan]::FromMinutes(5)) -LogPath (Join-Path $artifactRoot "logs/sourcelink-tool-install.log") | Out-Null
    }
    $sourcelinkTool = Join-Path $toolRoot $(if ($IsWindows) { 'sourcelink.exe' } else { 'sourcelink' })
    $symbolExtract = Join-Path $workRoot "$packageId-symbols"
    Expand-ZipArchive $snupkgPath $symbolExtract
    $pdbs = @(Get-ChildItem $symbolExtract -Filter *.pdb -File -Recurse)
    if (-not $pdbs) { throw "${packageId}: no portable PDB found in the symbol package." }
    foreach ($pdb in $pdbs) {
        $jsonResult = Invoke-NativeCommand -FilePath $sourcelinkTool -ArgumentList @('print-json', $pdb.FullName) -Timeout ([TimeSpan]::FromMinutes(1)) -LogPath (Join-Path $artifactRoot "logs/$packageId-$($pdb.Name)-json.log")
        if ($jsonResult.StandardOutput -notmatch 'raw\.githubusercontent\.com|github\.com/.*/raw') { throw "${packageId}: invalid SourceLink mapping in '$($pdb.Name)'." }
        if (-not $SkipRemoteSourceLinkRetrieval) {
            Invoke-NativeCommand -FilePath $sourcelinkTool -ArgumentList @('test', $pdb.FullName) -Timeout ([TimeSpan]::FromMinutes(2)) -LogPath (Join-Path $artifactRoot "logs/$packageId-$($pdb.Name)-remote.log") | Out-Null
        }
    }
    Write-Host "  snupkg: SourceLink mapping verified."

    # A minimal, real consumer: restore the packed .nupkg from an isolated local feed, build, and
    # run something that actually exercises the package's public API (not just loads the assembly).
    $consumerRoot = Join-Path $workRoot "$packageId-consumer"
    New-Item $consumerRoot -ItemType Directory -Force | Out-Null
    $globalPackages = Join-Path $workRoot "$packageId-global-packages"
    New-Item $globalPackages -ItemType Directory -Force | Out-Null
    $configPath = Join-Path $consumerRoot "NuGet.config"
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources><clear /><add key="provisional" value="$packageRoot" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping><clear /><packageSource key="provisional"><package pattern="omy.*" /></packageSource><packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping>
</configuration>
"@ | Set-Content $configPath
    @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include="$packageId" Version="$version" /></ItemGroup></Project>
"@ | Set-Content (Join-Path $consumerRoot "Consumer.csproj")

    # A generic "assembly loads" smoke test would not prove the package's actual public API is
    # usable from the packed .nupkg, so each provisional package gets a real, hand-written
    # exercise of one of its documented features. Adding a new provisional-package exclusion
    # requires adding a case here - failing closed (throwing on an unknown package) rather than
    # silently skipping functional verification for a package this script does not yet know.
    $consumerSource = switch ($packageId) {
        'omy.Utils.Collections' {
            @"
using Utils.Collections;
var list = new SkipList<int>();
list.Add(3); list.Add(1); list.Add(2);
if (list.Count != 3) throw new InvalidOperationException(`$"Expected 3 elements, got {list.Count}.");
int[] sorted = new int[list.Count];
list.CopyTo(sorted, 0);
if (sorted[0] != 1 || sorted[1] != 2 || sorted[2] != 3) throw new InvalidOperationException("SkipList did not maintain sorted order.");
Console.WriteLine("provisional-package-consumer-ok:" + string.Join(",", sorted));
"@
        }
        default { throw "${packageId}: no functional consumer is defined for this provisional package in eng/test-provisional-package.ps1 - add one before relying on this gate for it." }
    }
    $consumerSource | Set-Content (Join-Path $consumerRoot "Program.cs")
    $consumerProject = Join-Path $consumerRoot "Consumer.csproj"
    Invoke-NativeCommand -FilePath $dotnetPath -ArgumentList @('restore', $consumerProject, '--configfile', $configPath, '--packages', $globalPackages) -Timeout ([TimeSpan]::FromMinutes(5)) -LogPath (Join-Path $artifactRoot "logs/$packageId-consumer-restore.log") | Out-Null
    $env:NUGET_PACKAGES = $globalPackages
    try {
        Invoke-NativeCommand -FilePath $dotnetPath -ArgumentList @('build', $consumerProject, '--configuration', $Configuration, '--no-restore') -Timeout ([TimeSpan]::FromMinutes(5)) -LogPath (Join-Path $artifactRoot "logs/$packageId-consumer-build.log") | Out-Null
        $runResult = Invoke-NativeCommand -FilePath $dotnetPath -ArgumentList @('run', '--project', $consumerProject, '--configuration', $Configuration, '--no-build') -Timeout ([TimeSpan]::FromMinutes(2)) -LogPath (Join-Path $artifactRoot "logs/$packageId-consumer-run.log")
        if ($runResult.StandardOutput -notmatch 'provisional-package-consumer-ok:') { throw "${packageId}: consumer did not report success (expected 'provisional-package-consumer-ok:' in output)." }
    } finally {
        Remove-Item Env:\NUGET_PACKAGES -ErrorAction SilentlyContinue
    }
    Write-Host "  consumer: restored, built, and executed the packed nupkg successfully."
}
Write-Host "Provisional package validation passed for: $($provisionalPackages.packageId -join ', ')."
