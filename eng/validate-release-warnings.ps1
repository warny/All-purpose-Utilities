<#
.SYNOPSIS
Builds the solution while promoting warnings to errors only in product-train projects.
#>
[CmdletBinding()]
param([string] $Configuration = "Release", [string] $ArtifactsPath = "artifacts")
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot 'Release.Common.ps1')
$repoRoot=Split-Path -Parent $PSScriptRoot;$manifest=Get-ProductTrainManifest $repoRoot
$exceptions=Get-Content (Join-Path $PSScriptRoot 'release-warning-exceptions.json') -Raw|ConvertFrom-Json
$expired=@($exceptions.exceptions|Where-Object{[DateTime]$_.expiryDate-lt[DateTime]::UtcNow.Date});if($expired){throw "Expired warning exceptions: $($expired.project -join ', ')."}
$invalid=@($exceptions.exceptions|Where-Object{[string]::IsNullOrWhiteSpace($_.code)-or$_.code-match'\*'-or-not($manifest.packages.project-contains$_.project)-or[string]::IsNullOrWhiteSpace($_.trackingIssue)});if($invalid){throw 'Warning exceptions must be project-specific, non-wildcard, tracked, and unexpired.'}
$allAllowed=@($exceptions.exceptions.code|Select-Object -Unique)
$exceptionTargets = Resolve-RepositoryPath $repoRoot (Join-Path $ArtifactsPath 'release-warning-exceptions.targets')
$targetLines = @('<Project>')
foreach ($package in $manifest.packages) {
    $codes = @($exceptions.exceptions | Where-Object project -eq $package.project | ForEach-Object code)
    if ($codes) { $fullPath = (Resolve-RepositoryPath $repoRoot $package.project).Replace('&amp;', '&').Replace('&', '&amp;'); $targetLines += ('  <PropertyGroup Condition="''$(MSBuildProjectFullPath)'' == ''{0}''"><NoWarn>$(NoWarn);{1}</NoWarn></PropertyGroup>' -f $fullPath, ($codes -join ';')) }
}
$targetLines += '</Project>'; New-Item (Split-Path $exceptionTargets -Parent) -ItemType Directory -Force | Out-Null; $targetLines | Set-Content $exceptionTargets
$arguments=@('build',(Join-Path $repoRoot 'Utils.sln'),'--configuration',$Configuration,'--no-restore','--no-incremental','-p:ReleaseQualityGates=true','-p:UseSharedCompilation=false',"-p:CustomAfterMicrosoftCommonTargets=$exceptionTargets")
$output=& dotnet @arguments 2>&1;$exit=$LASTEXITCODE
if($exit-ne 0){$output|Write-Host;throw 'Release warning build failed.'}
$unexpected = @()
foreach ($line in $output) {
    $match = [regex]::Match([string]$line, 'warning ([A-Z]+\d+):.*\[([^]]+\.csproj)\]')
    if ($match.Success) { $relativeProject = Get-RepositoryRelativePath $repoRoot $match.Groups[2].Value; if ($manifest.packages.project -contains $relativeProject -and -not ($exceptions.exceptions | Where-Object { $_.project -eq $relativeProject -and $_.code -eq $match.Groups[1].Value })) { $unexpected += "${relativeProject}:$($match.Groups[1].Value)" } }
}
if($unexpected){throw "Unexpected release warnings: $($unexpected -join ', ')."}
$results=@($manifest.packages|ForEach-Object{[ordered]@{packageId=$_.packageId;project=$_.project;allowedWarnings=@($exceptions.exceptions|Where-Object project -eq $_.project|ForEach-Object code);unexpectedWarnings=@();passed=$true}})
Write-ReleaseJson ([ordered]@{version=[string]$manifest.version;releaseQualityGates=$true;packages=$results}) (Resolve-RepositoryPath $repoRoot (Join-Path $ArtifactsPath 'reports/warnings.json'))
