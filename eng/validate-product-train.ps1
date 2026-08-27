<#
.SYNOPSIS
Validates the single authoritative version of every manifested package project.
#>
[CmdletBinding()]
param([ValidateNotNullOrEmpty()][string] $Configuration = "Release")
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot 'Release.Common.ps1')
$repoRoot=Split-Path -Parent $PSScriptRoot;$manifest=Get-ProductTrainManifest $repoRoot
[xml]$props=Get-Content (Join-Path $repoRoot 'Directory.Build.props') -Raw;$declared=([string]$props.Project.PropertyGroup.($manifest.versionProperty)).Trim()
if($declared-ne$manifest.version){throw "Manifest version '$($manifest.version)' differs from $($manifest.versionProperty) '$declared'."}
foreach($package in $manifest.packages){
    $path=Resolve-RepositoryPath $repoRoot $package.project;[xml]$xml=Get-Content $path -Raw;$nodes=@($xml.SelectNodes('/Project/PropertyGroup/Version'))
    if($nodes.Count-ne 1-or$nodes[0].InnerText.Trim()-ne"`$($($manifest.versionProperty))"){throw "$($package.project) must declare <Version>`$($($manifest.versionProperty))</Version> exactly once."}
    $raw=Get-Content $path -Raw
    if($raw-match'<(?:Version|PackageVersion|VersionPrefix|VersionSuffix|AssemblyVersion|FileVersion|InformationalVersion)>\s*\d'){throw "$($package.project) contains a hard-coded version override."}
    if(@($xml.SelectNodes('/Project/PropertyGroup/PackageVersion|/Project/PropertyGroup/VersionPrefix|/Project/PropertyGroup/VersionSuffix|/Project/PropertyGroup/AssemblyVersion|/Project/PropertyGroup/FileVersion|/Project/PropertyGroup/InformationalVersion')).Count){throw "$($package.project) contains a forbidden local version property."}
    $evaluated=Get-EvaluatedProject $path $Configuration;$p=$evaluated.Properties
    if($p.PackageId-ne$package.packageId-or$p.IsPackable-eq'false'){throw "$($package.project) evaluates an invalid package identity or IsPackable value."}
    if($p.Version-ne$manifest.version-or$p.PackageVersion-ne$manifest.version){throw "$($package.packageId) evaluates divergent Version/PackageVersion values (expected '$($manifest.version)')."}
    if($p.VersionPrefix-or$p.VersionSuffix){throw "$($package.packageId) must not compose the candidate from VersionPrefix/VersionSuffix."}
    $readmePath=Join-Path (Split-Path -Parent $path) 'README.md'
    if(Test-Path -LiteralPath $readmePath -PathType Leaf){
        $readme=Get-Content -LiteralPath $readmePath -Raw
        # Catches a doubled-suffix concatenation bug (for example "2.0.0-rc.1-rc.1") that a
        # packed README would otherwise ship to consumers verbatim.
        if($readme-match"$([regex]::Escape($manifest.version))-rc\.\d+"){throw "$($package.packageId): README.md contains a doubled prerelease suffix ('$($manifest.version)-rc.N') - check for a stale concatenated version string."}
    }
}
Write-Host "Validate: all $($manifest.packages.Count) projects evaluate to $($manifest.versionProperty)=$($manifest.version)."
