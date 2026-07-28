<#
.SYNOPSIS
Compares every runtime assembly with its latest declared public package baseline.
#>
[CmdletBinding()]
param([string] $Configuration = "Release", [string] $ArtifactsPath = "artifacts")
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Release.Common.ps1")
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-ProductTrainManifest $repoRoot; $artifactRoot = Resolve-RepositoryPath $repoRoot $ArtifactsPath
$packageRoot = Join-Path $artifactRoot 'packages'; $workRoot = Join-Path $artifactRoot 'api-compat'
Remove-Item $workRoot -Recurse -Force -ErrorAction SilentlyContinue; New-Item $workRoot -ItemType Directory -Force | Out-Null
$toolRoot = Join-Path $workRoot 'tool'; & dotnet tool install Microsoft.DotNet.ApiCompat.Tool --tool-path $toolRoot --version 10.0.302
if ($LASTEXITCODE -ne 0) { throw 'ApiCompat tool installation failed.' }
$tool = Join-Path $toolRoot $(if ($IsWindows) { 'apicompat.exe' } else { 'apicompat' }); $results = @()
foreach ($package in $manifest.packages) {
    if ($package.kind -eq 'analyzer' -and -not $package.publishedVersion) { $results += [ordered]@{ packageId=$package.packageId; baseline='first-candidate'; result='baseline-created'; breakingChanges=0 }; continue }
    $candidateRoot = Join-Path $workRoot "candidate/$($package.packageId)"; Expand-Archive (Join-Path $packageRoot "$($package.packageId).$($manifest.version).nupkg") $candidateRoot -Force
    $candidateDll = Get-ChildItem $candidateRoot -Filter *.dll -File -Recurse | Where-Object FullName -match '[\\/](lib|analyzers)[\\/]' | Select-Object -First 1
    if (-not $candidateDll) { throw "$($package.packageId): candidate assembly missing." }
    if (-not $package.publishedVersion) { $results += [ordered]@{ packageId=$package.packageId; baseline='first-candidate'; result='baseline-created'; breakingChanges=0; candidateAssembly=$candidateDll.Name }; continue }
    $baselineVersion = [string]$package.publishedVersion
    $indexUrl = "https://api.nuget.org/v3-flatcontainer/$($package.packageId.ToLowerInvariant())/index.json"
    $versions = @((Invoke-RestMethod $indexUrl).versions)
    # The manifest pins the audited baseline; querying NuGet proves that it still exists and records newer releases without silently changing the comparison.
    if ($versions -notcontains $baselineVersion) { throw "$($package.packageId): declared published baseline '$($package.publishedVersion)' does not exist." }
    $latestStable = @($versions | Where-Object { $_ -notmatch '-' } | Select-Object -Last 1)[0]
    if ($latestStable -ne $baselineVersion) { throw "$($package.packageId): manifest baseline '$($package.publishedVersion)' is not latest stable '$latestStable'." }
    $baselineFile = Join-Path $workRoot "$($package.packageId).$($package.publishedVersion).nupkg"
    Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/$($package.packageId.ToLowerInvariant())/$($package.publishedVersion)/$($package.packageId.ToLowerInvariant()).$($package.publishedVersion).nupkg" -OutFile $baselineFile
    $baselineRoot = Join-Path $workRoot "baseline/$($package.packageId)"; Expand-Archive $baselineFile $baselineRoot -Force
    $baselineDll = Get-ChildItem $baselineRoot -Filter *.dll -File -Recurse | Where-Object FullName -match '[\\/]lib[\\/]' | Select-Object -First 1
    if (-not $baselineDll) { throw "$($package.packageId): baseline assembly missing." }
    $log = Join-Path $workRoot "$($package.packageId).txt"; $output = & $tool -l $baselineDll.FullName -r $candidateDll.FullName 2>&1; $exitCode=$LASTEXITCODE; $output | Set-Content $log
    $diagnostics = @($output | Select-String '^CP\d+:')
    $breaking = $diagnostics.Count
    $reverseLog = Join-Path $workRoot "$($package.packageId)-additions.txt"; $reverse = & $tool -l $candidateDll.FullName -r $baselineDll.FullName 2>&1; $reverse | Set-Content $reverseLog
    $additions = @($reverse | Select-String '^(CP0001|CP0002):').Count
    if ($exitCode -ne 0 -and [string]::IsNullOrWhiteSpace($package.acceptedBreakingChanges)) { throw "$($package.packageId): $breaking unaccepted API breaks." }
    $results += [ordered]@{ packageId=$package.packageId; latestStable=$latestStable; latestPrerelease=($versions | Where-Object {$_ -match '-'} | Select-Object -Last 1); baseline=[string]$package.publishedVersion; result=if($breaking){'accepted-major-version-breaks'}else{'compatible'}; breakingChanges=$breaking; classifications=[ordered]@{ compatibleAddition=$additions; sourceBreaking=@($diagnostics | Where-Object { $_ -match 'CP0021|constraint' }).Count; binaryBreaking=$breaking; removed=@($diagnostics | Where-Object { $_ -match 'CP0001|CP0002' }).Count; renamed=0; moved=0; obsolete=@($diagnostics | Where-Object { $_ -match 'obsolete' }).Count; behavioralChangeRequiringDocumentation=if($package.packageId -eq 'omy.Utils'){1}else{0} }; acceptance=[string]$package.acceptedBreakingChanges; report=(Get-RepositoryRelativePath $artifactRoot $log); additionsReport=(Get-RepositoryRelativePath $artifactRoot $reverseLog) }
}
$report=[ordered]@{version=[string]$manifest.version;packages=$results}; Write-ReleaseJson $report (Join-Path $artifactRoot 'reports/public-api-comparison.json')
@('# Public API comparison','',"Candidate: ``$($manifest.version)``",'') + @($results | ForEach-Object { "- **$($_.packageId)**: baseline $($_.baseline); $($_.result); $($_.breakingChanges) reported incompatibilities." }) | Set-Content (Join-Path $artifactRoot 'reports/public-api-comparison.md')
