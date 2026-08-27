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
    $policy=Get-PackageVersionPolicy $package;$expectedVersion=Get-PackageVersion $manifest $package
    $path=Resolve-RepositoryPath $repoRoot $package.project;[xml]$xml=Get-Content $path -Raw;$nodes=@($xml.SelectNodes('/Project/PropertyGroup/Version'))
    if($policy-eq'product-train'){
        if($nodes.Count-ne 1-or$nodes[0].InnerText.Trim()-ne"`$($($manifest.versionProperty))"){throw "$($package.project) must declare <Version>`$($($manifest.versionProperty))</Version> exactly once."}
    } else {
        if($nodes.Count-ne 1-or$nodes[0].InnerText.Trim()-ne$expectedVersion){throw "$($package.project) has versionPolicy '$policy' and must declare <Version>$expectedVersion</Version> literally, exactly once."}
    }
    $raw=Get-Content $path -Raw
    if($policy-eq'product-train'){
        if($raw-match'<(?:Version|PackageVersion|VersionPrefix|VersionSuffix|AssemblyVersion|FileVersion|InformationalVersion)>\s*\d'){throw "$($package.project) contains a hard-coded version override."}
    } else {
        if($raw-match'<(?:PackageVersion|VersionPrefix|VersionSuffix|AssemblyVersion|FileVersion|InformationalVersion)>\s*\d'){throw "$($package.project) contains a hard-coded version override beyond its declared provisional <Version>."}
    }
    if(@($xml.SelectNodes('/Project/PropertyGroup/PackageVersion|/Project/PropertyGroup/VersionPrefix|/Project/PropertyGroup/VersionSuffix|/Project/PropertyGroup/AssemblyVersion|/Project/PropertyGroup/FileVersion|/Project/PropertyGroup/InformationalVersion')).Count){throw "$($package.project) contains a forbidden local version property."}
    $evaluated=Get-EvaluatedProject $path $Configuration;$p=$evaluated.Properties
    if($p.PackageId-ne$package.packageId-or$p.IsPackable-eq'false'){throw "$($package.project) evaluates an invalid package identity or IsPackable value."}
    if($p.Version-ne$expectedVersion-or$p.PackageVersion-ne$expectedVersion){throw "$($package.packageId) evaluates divergent Version/PackageVersion values (expected '$expectedVersion' under versionPolicy '$policy')."}
    if($p.VersionPrefix-or$p.VersionSuffix){throw "$($package.packageId) must not compose the candidate from VersionPrefix/VersionSuffix."}
}
Write-Host "Validate: all $($manifest.packages.Count) projects evaluate to their declared versionPolicy version ($($manifest.versionProperty)=$($manifest.version) for product-train packages, explicit provisional versions otherwise)."
