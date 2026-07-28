<#
.SYNOPSIS
Performs two clean builds of the complete train and compares every package entry.
#>
[CmdletBinding()]
param([string] $Configuration = "Release", [string] $ArtifactsPath = "artifacts")
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem
. (Join-Path $PSScriptRoot "Release.Common.ps1")
$repoRoot=Split-Path -Parent $PSScriptRoot; $manifest=Get-ProductTrainManifest $repoRoot; $artifactRoot=Resolve-RepositoryPath $repoRoot $ArtifactsPath
<# Returns content hashes for meaningful entries while excluding ZIP container metadata. #>
function Get-ArchiveEntries { param([string]$Path) $zip=[IO.Compression.ZipFile]::OpenRead($Path);$sha=[Security.Cryptography.SHA256]::Create();try{$values=@{};foreach($entry in $zip.Entries){if($entry.FullName -eq '.signature.p7s' -or $entry.FullName -eq '_rels/.rels' -or $entry.FullName -like 'package/services/metadata/core-properties/*'){continue};$stream=$entry.Open();try{$values[$entry.FullName]=[Convert]::ToHexString($sha.ComputeHash($stream)).ToLowerInvariant()}finally{$stream.Dispose()}};return $values}finally{$sha.Dispose();$zip.Dispose()}}
foreach($run in 1..2){
    foreach($package in $manifest.packages){& dotnet clean (Resolve-RepositoryPath $repoRoot $package.project) --configuration $Configuration | Out-Null;if($LASTEXITCODE-ne 0){throw "Clean failed for $($package.project)."}}
    & (Join-Path $PSScriptRoot 'pack-product-train.ps1') -Configuration $Configuration -ArtifactsPath (Join-Path $ArtifactsPath "reproducibility/run$run")
    if($LASTEXITCODE-ne 0){throw "Reproducibility build $run failed."}
}
$comparisons=@()
foreach($package in $manifest.packages){foreach($extension in @('nupkg','snupkg')){$name="$($package.packageId).$($manifest.version).$extension";$one=Join-Path $artifactRoot "reproducibility/run1/packages/$name";$two=Join-Path $artifactRoot "reproducibility/run2/packages/$name";if(-not(Test-Path $one)-or -not (Test-Path $two)){throw "$name is missing from one build."};$hashOne=(Get-FileHash $one -Algorithm SHA256).Hash.ToLowerInvariant();$hashTwo=(Get-FileHash $two -Algorithm SHA256).Hash.ToLowerInvariant();$entriesOne=Get-ArchiveEntries $one;$entriesTwo=Get-ArchiveEntries $two;$differences=@();foreach($path in @($entriesOne.Keys+$entriesTwo.Keys|Sort-Object -Unique)){if($entriesOne[$path]-ne$entriesTwo[$path]){$differences += [ordered]@{file=$path;firstHash=$entriesOne[$path];secondHash=$entriesTwo[$path];difference='content'}}};$result=if($hashOne-eq$hashTwo){'bit-identical'}elseif(-not$differences){'logically-identical-after-zip-normalization'}else{'different'};if($result-eq'different'){throw "$name contains reproducibility differences: $($differences.file -join ', ')."};$comparisons += [ordered]@{packageId=$package.packageId;version=[string]$manifest.version;artifact=$name;result=$result;firstSha256=$hashOne;secondSha256=$hashTwo;differences=$differences}}}
Write-ReleaseJson ([ordered]@{productTrain=[string]$manifest.productTrain;version=[string]$manifest.version;artifacts=$comparisons}) (Join-Path $artifactRoot 'reports/reproducibility-report.json')
