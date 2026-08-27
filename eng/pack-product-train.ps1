<#
.SYNOPSIS
Builds and packs the complete manifested product train without publishing.
#>
[CmdletBinding()]
param([ValidateNotNullOrEmpty()][string] $Configuration = "Release", [string] $ArtifactsPath = "artifacts", [switch] $NoBuild)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Release.Common.ps1")
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-ProductTrainManifest $repoRoot
$artifactRoot = Resolve-RepositoryPath $repoRoot $ArtifactsPath
$packagePath = Join-Path $artifactRoot "packages"
$logRoot = Join-Path $artifactRoot "logs/canonical-packaging"
$dotnetPath = @(Get-Command dotnet -CommandType Application)[0].Source
& (Join-Path $PSScriptRoot "validate-product-train.ps1") -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { throw "Product version validation failed." }
& (Join-Path $PSScriptRoot "analyze-package-graph.ps1") -Configuration $Configuration -ArtifactsPath $ArtifactsPath
if ($LASTEXITCODE -ne 0) { throw "Package graph validation failed." }
$order = @(Get-Content (Join-Path $artifactRoot "reports/package-publication-order.txt"))
$byId = @{}; $manifest.packages | ForEach-Object { $byId[$_.packageId] = $_ }
if (Test-Path $packagePath) { Remove-Item $packagePath -Recurse -Force }
New-Item $packagePath -ItemType Directory -Force | Out-Null
if (-not $NoBuild) {
    foreach ($id in $order) {
        $package = $byId[$id]
        $buildLog = Join-Path $logRoot "build-$($package.packageId).log"
        Invoke-NativeCommand -FilePath $dotnetPath -ArgumentList @("build", (Resolve-RepositoryPath $repoRoot $package.project), "--configuration", $Configuration, "--no-restore", "-p:ContinuousIntegrationBuild=true", "-p:UseSharedCompilation=false") -Timeout ([TimeSpan]::FromMinutes(15)) -LogPath $buildLog | Out-Null
    }
}
foreach ($id in $order) {
    $package = $byId[$id]
    Write-Host "Pack: $($package.packageId) from $($package.project)"
    $packLog = Join-Path $logRoot "pack-$($package.packageId).log"
    Invoke-NativeCommand -FilePath $dotnetPath -ArgumentList @("pack", (Resolve-RepositoryPath $repoRoot $package.project), "--configuration", $Configuration, "--no-build", "--no-restore", "--output", $packagePath, "-p:ContinuousIntegrationBuild=true") -Timeout ([TimeSpan]::FromMinutes(15)) -LogPath $packLog | Out-Null
}

# NuGet serializes project-reference dependencies as minimum versions. Rewrite only
# manifested internal dependencies to exact ranges before these archives become candidates.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$byLowerId = @{}; $manifest.packages | ForEach-Object { $byLowerId[$_.packageId.ToLowerInvariant()] = $_ }
$internalIds = @($byLowerId.Keys)
foreach ($archivePath in Get-ChildItem $packagePath -File | Where-Object Extension -in @('.nupkg', '.snupkg')) {
    $archive = [IO.Compression.ZipFile]::Open($archivePath.FullName, [IO.Compression.ZipArchiveMode]::Update)
    try {
        $nuspecEntry = $archive.Entries | Where-Object FullName -like '*.nuspec' | Select-Object -First 1
        $reader = [IO.StreamReader]::new($nuspecEntry.Open()); try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $changed = $false
        foreach ($dependency in $nuspec.SelectNodes("//*[local-name()='dependency']")) {
            $lowerId = $dependency.id.ToLowerInvariant()
            if ($internalIds -contains $lowerId) { $dependency.version = "[$($manifest.version)]"; $changed = $true }
        }
        if ($changed) {
            $name = $nuspecEntry.FullName; $nuspecEntry.Delete(); $replacement = $archive.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
            $writer = [IO.StreamWriter]::new($replacement.Open(), [Text.UTF8Encoding]::new($false)); try { $nuspec.Save($writer) } finally { $writer.Dispose() }
        }
    } finally { $archive.Dispose() }
}

$actual = @(Get-ChildItem $packagePath -Filter *.nupkg -File | Where-Object Extension -eq '.nupkg')
if ($actual.Count -ne $manifest.packages.Count) { throw "Expected $($manifest.packages.Count) packages, found $($actual.Count)." }
foreach ($package in $manifest.packages) {
    $expected = Join-Path $packagePath "$($package.packageId).$($manifest.version).nupkg"
    if (-not (Test-Path $expected)) { throw "Expected package '$expected' is missing." }
}
Write-Host "Pack: complete train staged; no package was published."
