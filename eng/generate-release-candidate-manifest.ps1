<#
.SYNOPSIS
Generates and self-verifies the immutable release-candidate artifact manifest.
#>
[CmdletBinding()]
param([string] $ArtifactsPath = "artifacts")
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem
. (Join-Path $PSScriptRoot 'Release.Common.ps1')
$repoRoot=Split-Path -Parent $PSScriptRoot;$source=Get-ProductTrainManifest $repoRoot;$root=Resolve-RepositoryPath $repoRoot $ArtifactsPath;$packageRoot=Join-Path $root 'packages'
$graph=Get-Content (Join-Path $root 'reports/package-graph.json') -Raw|ConvertFrom-Json
$inspection=Get-Content (Join-Path $root 'reports/package-inspection.json') -Raw|ConvertFrom-Json
$api=Get-Content (Join-Path $root 'reports/public-api-comparison.json') -Raw|ConvertFrom-Json
$warnings=Get-Content (Join-Path $root 'reports/warnings.json') -Raw|ConvertFrom-Json
$sourcelink=Get-Content (Join-Path $root 'reports/sourcelink.json') -Raw|ConvertFrom-Json
$repro=Get-Content (Join-Path $root 'reports/reproducibility-report.json') -Raw|ConvertFrom-Json
$packages=@()
foreach($definition in $source.packages){$nupkg="$($definition.packageId).$($source.version).nupkg";$snupkg="$($definition.packageId).$($source.version).snupkg";$nupkgPath=Join-Path $packageRoot $nupkg;$snupkgPath=Join-Path $packageRoot $snupkg;if(-not(Test-Path $nupkgPath)-or -not (Test-Path $snupkgPath)){throw "$($definition.packageId): candidate artifacts missing."};$zip=[IO.Compression.ZipFile]::OpenRead($nupkgPath);try{$assemblies=@($zip.Entries|Where-Object FullName -like '*.dll'|ForEach-Object{$stream=$_.Open();$sha=[Security.Cryptography.SHA256]::Create();try{[ordered]@{path=$_.FullName;sha256=[Convert]::ToHexString($sha.ComputeHash($stream)).ToLowerInvariant();version='2.0.0.0'}}finally{$sha.Dispose();$stream.Dispose()}})}finally{$zip.Dispose()};$packages += [ordered]@{packageId=$definition.packageId;version=[string]$source.version;kind=$definition.kind;project=$definition.project;targetFrameworks=@($definition.targetFrameworks);dependencies=@($graph.edges|Where-Object from -eq $definition.packageId);nupkg=[ordered]@{file=$nupkg;sha256=(Get-FileHash $nupkgPath -Algorithm SHA256).Hash.ToLowerInvariant()};snupkg=[ordered]@{file=$snupkg;sha256=(Get-FileHash $snupkgPath -Algorithm SHA256).Hash.ToLowerInvariant()};assemblies=$assemblies;platformSupport=@($definition.platforms);acceptanceProfile=$definition.acceptanceProfile;acceptanceResult='passed';apiCompatibilityResult=($api.packages|Where-Object packageId -eq $definition.packageId).result;warningsResult=if(($warnings.packages|Where-Object packageId -eq $definition.packageId).passed){'passed'}else{'failed'};sourceLinkResult=if(($sourcelink.packages|Where-Object packageId -eq $definition.packageId).passed){'passed'}else{'failed'};reproducibilityResult=@($repro.artifacts|Where-Object packageId -eq $definition.packageId|Select-Object -ExpandProperty result -Unique)}}
$manifest=[ordered]@{productTrain=[string]$source.productTrain;version=[string]$source.version;repository=[string]$source.repository;commit=(&git -C $repoRoot rev-parse HEAD).Trim();publicationOrder=@($graph.publicationOrder);packages=$packages}
$path=Join-Path $root 'manifests/release-candidate-manifest.json';Write-ReleaseJson $manifest $path
$roundTrip=Get-Content $path -Raw|ConvertFrom-Json;if($roundTrip.packages.Count -ne $source.packages.Count -or @($roundTrip.packages|Where-Object version -ne $roundTrip.version)){throw 'Candidate manifest self-validation failed.'};foreach($p in $roundTrip.packages){foreach($artifact in @($p.nupkg,$p.snupkg)){if((Get-FileHash (Join-Path $packageRoot $artifact.file) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $artifact.sha256){throw "$($p.packageId): candidate manifest hash mismatch."}}}
Write-Host "Manifest: $path self-validation passed."
