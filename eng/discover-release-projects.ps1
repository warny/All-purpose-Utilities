<#
.SYNOPSIS
Discovers and classifies every MSBuild project relevant to repository releases.
#>
[CmdletBinding()]
param([string] $Configuration = "Release", [string] $ArtifactsPath = "artifacts")
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Release.Common.ps1")
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-ProductTrainManifest $repoRoot
$manifestProjects = @{}; $manifest.packages | ForEach-Object { $manifestProjects[$_.project] = $_ }
$excludedProjects = @{}; $manifest.exclusions | ForEach-Object { $excludedProjects[$_.project] = $_ }
$projects = @(Get-ChildItem $repoRoot -Filter *.csproj -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj|artifacts|\.git|\.worktrees?)[\\/]' })
$results = @()
foreach ($file in $projects) {
    $relative = Get-RepositoryRelativePath $repoRoot $file.FullName
    $evaluation = Get-EvaluatedProject $file.FullName $Configuration
    $p = $evaluation.Properties
    $classification = if ($manifestProjects.ContainsKey($relative)) {
        if ($manifestProjects[$relative].kind -eq "analyzer") { "analyzer-package" } else { "library-package" }
    } elseif ($excludedProjects.ContainsKey($relative)) { [string]$excludedProjects[$relative].classification }
    elseif ($p.IsPackable -eq "false") { "non-packable" }
    elseif ($p.PackageType -match "VSIX") { "vsix" }
    elseif ($p.IsRoslynAnalyzer -eq "true") { "analyzer-package" }
    elseif ($p.OutputType -in @("Exe", "WinExe")) { "application" }
    elseif (-not [string]::IsNullOrWhiteSpace($p.PackageId)) { "ambiguous" }
    else { "non-packable" }
    $results += [ordered]@{
        project = $relative; packageId = [string]$p.PackageId; classification = $classification
        version = [string]$p.Version; packageVersion = [string]$p.PackageVersion; isPackable = [string]$p.IsPackable
        targetFramework = [string]$p.TargetFramework; targetFrameworks = [string]$p.TargetFrameworks
        packageType = [string]$p.PackageType; generatePackageOnBuild = [string]$p.GeneratePackageOnBuild
        isRoslynAnalyzer = [string]$p.IsRoslynAnalyzer; buildOutputTargetFolder = [string]$p.BuildOutputTargetFolder
    }
}
$duplicates = @($results | Where-Object classification -in @("library-package", "analyzer-package") | Group-Object { $_.packageId.ToLowerInvariant() } | Where-Object Count -gt 1)
$ambiguous = @($results | Where-Object classification -eq "ambiguous")
$missing = @($manifest.packages | Where-Object { -not ($results.project -contains $_.project) -or ($results | Where-Object project -eq $_.project).isPackable -eq "false" })
if ($duplicates) { throw "Duplicate package IDs: $($duplicates.Name -join ', ')." }
if ($ambiguous) { throw "Projects require an explicit manifest decision: $($ambiguous.project -join ', ')." }
if ($missing) { throw "Manifest projects are missing or non-packable: $($missing.project -join ', ')." }
$reportRoot = Resolve-RepositoryPath $repoRoot (Join-Path $ArtifactsPath "reports")
Write-ReleaseJson ([ordered]@{ generatedAtUtc = [DateTime]::UtcNow.ToString('O'); projects = $results }) (Join-Path $reportRoot "release-project-discovery.json")
$results | ForEach-Object { "$($_.classification)`t$($_.project)`t$($_.packageId)`t$($_.packageVersion)`t$($_.targetFramework)$($_.targetFrameworks)" } | Set-Content (Join-Path $reportRoot "release-project-discovery.txt")
Write-Host "Discovery: $($manifest.packages.Count) product packages and $($manifest.exclusions.Count) explicit exclusions validated."
