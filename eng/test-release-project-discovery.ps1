<#
.SYNOPSIS
Verifies that release discovery rejects a packable project omitted from the manifest.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$probeRoot = Join-Path $repoRoot "ReleaseDiscoveryProbe-$([guid]::NewGuid().ToString('N'))"
$artifactsPath = "artifacts/release-discovery-test-$([guid]::NewGuid().ToString('N'))"
try {
    New-Item $probeRoot -ItemType Directory -Force | Out-Null
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>true</IsPackable>
    <PackageId>Undeclared.Release.Discovery.Probe</PackageId>
  </PropertyGroup>
</Project>
"@ | Set-Content (Join-Path $probeRoot "Undeclared.Release.Discovery.Probe.csproj")
    try {
        & (Join-Path $PSScriptRoot "discover-release-projects.ps1") -Configuration Release -ArtifactsPath $artifactsPath
        throw "Release discovery accepted an undeclared packable project."
    } catch {
        if ($_.Exception.Message -notmatch "Projects require an explicit manifest decision" -or $_.Exception.Message -notmatch "Undeclared.Release.Discovery.Probe.csproj") { throw }
    }
} finally {
    Remove-Item $probeRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $repoRoot $artifactsPath) -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "Release project discovery tests passed."
