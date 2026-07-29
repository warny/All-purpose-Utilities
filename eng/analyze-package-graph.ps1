<#
.SYNOPSIS
Builds and validates the complete product-package dependency graph.
#>
[CmdletBinding()]
param([string] $Configuration = "Release", [string] $ArtifactsPath = "artifacts")
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Release.Common.ps1")
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-ProductTrainManifest $repoRoot
$byProject = @{}; $manifest.packages | ForEach-Object { $byProject[$_.project.ToLowerInvariant()] = $_ }
$edges = @()
foreach ($package in $manifest.packages) {
    $projectPath = Resolve-RepositoryPath $repoRoot $package.project
    $evaluation = Get-EvaluatedProject $projectPath $Configuration
    foreach ($reference in @($evaluation.Items.ProjectReference)) {
        $targetPath = Get-RepositoryRelativePath $repoRoot $reference.FullPath
        if (-not $byProject.ContainsKey($targetPath.ToLowerInvariant())) { throw "$($package.project) references non-product project '$targetPath'." }
        $target = $byProject[$targetPath.ToLowerInvariant()]
        $isAnalyzerReference = $reference.OutputItemType -eq "Analyzer" -or $reference.ReferenceOutputAssembly -eq "false" -or $target.kind -eq "analyzer"
        $relationship = if ($package.kind -eq "analyzer" -and $evaluation.Properties.SuppressDependenciesWhenPacking -eq "true") { "embedded analyzer dependency" }
            elseif ($isAnalyzerReference) { "private build dependency" } else { "NuGet runtime dependency" }
        $edges += [ordered]@{ from = [string]$package.packageId; to = [string]$target.packageId; relationship = $relationship; projectReference = $targetPath }
    }
}
$dependencies = @{}; foreach ($p in $manifest.packages) { $dependencies[$p.packageId] = @($edges | Where-Object from -eq $p.packageId | ForEach-Object to | Select-Object -Unique) }
$order = [Collections.Generic.List[string]]::new(); $remaining = [Collections.Generic.HashSet[string]]::new([string[]]$manifest.packages.packageId, [StringComparer]::OrdinalIgnoreCase)
while ($remaining.Count) {
    $ready = @($manifest.packages.packageId | Where-Object { $remaining.Contains($_) -and @($dependencies[$_] | Where-Object { $remaining.Contains($_) }).Count -eq 0 })
    if (-not $ready) { throw "The package graph contains a cycle." }
    foreach ($id in $ready) { $order.Add($id); $remaining.Remove($id) | Out-Null }
}
$reportRoot = Resolve-RepositoryPath $repoRoot (Join-Path $ArtifactsPath "reports")
Write-ReleaseJson ([ordered]@{ version = [string]$manifest.version; packages = @($manifest.packages.packageId); edges = $edges; publicationOrder = @($order) }) (Join-Path $reportRoot "package-graph.json")
@("digraph packages {") + @($edges | ForEach-Object { "    `"$($_.to)`" -> `"$($_.from)`" [label=`"$($_.relationship)`"];" }) + @("}") | Set-Content (Join-Path $reportRoot "package-graph.dot")
$order | Set-Content (Join-Path $reportRoot "package-publication-order.txt")
Write-Host "Graph: $($manifest.packages.Count) packages, $($edges.Count) edges, acyclic order generated."
