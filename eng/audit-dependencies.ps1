<#
.SYNOPSIS
Audits vulnerable, deprecated, and outdated dependencies for the solution and product train.
#>
[CmdletBinding()]
param([string] $ArtifactsPath = "artifacts")
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Release.Common.ps1")
$repoRoot = Split-Path -Parent $PSScriptRoot; $artifactRoot = Resolve-RepositoryPath $repoRoot $ArtifactsPath
$exceptions = Get-Content (Join-Path $PSScriptRoot 'dependency-exceptions.json') -Raw | ConvertFrom-Json
if (@($exceptions.exceptions | Where-Object { [DateTime]$_.expiryDate -lt [DateTime]::UtcNow.Date })) { throw 'A dependency exception has expired.' }
$reports=@(); foreach($mode in @(@('vulnerable','--vulnerable','--include-transitive'),@('deprecated','--deprecated'),@('outdated','--outdated'))){
    $name=$mode[0]; $args=@('list',(Join-Path $repoRoot 'Utils.sln'),'package')+$mode[1..($mode.Count-1)]; $output=& dotnet @args 2>&1; $exit=$LASTEXITCODE
    $path=Join-Path $artifactRoot "reports/dependencies-$name.log"; New-Item (Split-Path $path -Parent) -ItemType Directory -Force | Out-Null; $output|Set-Content $path
    if($exit-ne 0){throw "Dependency $name audit failed."}
    $reports += [ordered]@{kind=$name;log=(Get-RepositoryRelativePath $artifactRoot $path);exitCode=$exit}
}
# Vulnerabilities in manifested project dependency closures are checked independently of excluded VSIX/test projects.
$manifest=Get-ProductTrainManifest $repoRoot
foreach($package in $manifest.packages){$output=& dotnet list (Resolve-RepositoryPath $repoRoot $package.project) package --vulnerable --include-transitive 2>&1;if($LASTEXITCODE-ne 0 -or ($output -match '^\s*>\s+\S+\s+\S+\s+(Moderate|High|Critical)')){$output|Write-Host;throw "Blocking vulnerability for $($package.packageId)."}}
Write-ReleaseJson ([ordered]@{generatedAtUtc=[DateTime]::UtcNow.ToString('O');classification=@('product package','analyzer package','tooling only','test only','VSIX','transitive','direct');reports=$reports;exceptions=$exceptions.exceptions}) (Join-Path $artifactRoot 'reports/dependency-audit.json')
