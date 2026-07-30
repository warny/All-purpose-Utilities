<#
.SYNOPSIS
Audits vulnerable, deprecated, and outdated dependencies for the solution.
.DESCRIPTION
The solution report covers the dependency closures of its projects once, avoiding a
second network query for every product package. Vulnerabilities of moderate or greater
severity are blocking; deprecated and outdated reports can be disabled independently.
#>
[CmdletBinding()]
param(
    [string] $ArtifactsPath = "artifacts",
    [switch] $SkipDeprecated,
    [switch] $SkipOutdated,
    [switch] $PlanOnly,
    [TimeSpan] $VulnerabilityTimeout = ([TimeSpan]::FromMinutes(10)),
    [TimeSpan] $AdvisoryTimeout = ([TimeSpan]::FromMinutes(15))
)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Release.Common.ps1")
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Resolve-RepositoryPath $repoRoot $ArtifactsPath
$exceptions = Get-Content (Join-Path $PSScriptRoot "dependency-exceptions.json") -Raw | ConvertFrom-Json
if (@($exceptions.exceptions | Where-Object { [DateTime]$_.expiryDate -lt [DateTime]::UtcNow.Date })) {
    throw "A dependency exception has expired."
}

$auditModes = [Collections.Generic.List[object]]::new()
$auditModes.Add([pscustomobject]@{
    Name = "vulnerable"
    Arguments = [string[]]@("--vulnerable", "--include-transitive")
    Timeout = $VulnerabilityTimeout
    Blocking = $true
})
if (-not $SkipDeprecated) {
    $auditModes.Add([pscustomobject]@{
        Name = "deprecated"
        Arguments = [string[]]@("--deprecated")
        Timeout = $AdvisoryTimeout
        Blocking = $false
    })
}
if (-not $SkipOutdated) {
    $auditModes.Add([pscustomobject]@{
        Name = "outdated"
        Arguments = [string[]]@("--outdated")
        Timeout = $AdvisoryTimeout
        Blocking = $false
    })
}

if ($PlanOnly) {
    $auditModes | ForEach-Object { Write-Host "AUDIT $($_.Name)" }
    return
}

$reports = @()
foreach ($mode in $auditModes) {
    $path = Join-Path $artifactRoot "reports/dependencies-$($mode.Name).log"
    $arguments = [string[]](@("list", (Join-Path $repoRoot "Utils.sln"), "package") + $mode.Arguments)
    $result = Invoke-NativeCommand -FilePath "dotnet" -ArgumentList $arguments -Timeout $mode.Timeout -LogPath $path
    if ($mode.Blocking) {
        # Derive product-package findings from the single solution report. Tooling,
        # test, and VSIX dependencies remain visible in the log but do not change
        # the established blocking policy for candidate NuGet package closures.
        $manifest = Get-ProductTrainManifest $repoRoot
        foreach ($package in $manifest.packages) {
            $projectName = [IO.Path]::GetFileNameWithoutExtension([string]$package.project)
            $escapedName = [regex]::Escape($projectName)
            $sectionPattern = "(?ms)(?:Project|The given project) ``$escapedName``(?<section>.*?)(?=\r?\n(?:Project|The given project) ``|\z)"
            $section = [regex]::Match($result.StandardOutput, $sectionPattern)
            if (-not $section.Success) {
                throw "Dependency audit did not report manifested project '$projectName'. Log: $path"
            }
            if ($section.Groups["section"].Value -match '(?im)^\s*>\s+\S+\s+\S+\s+(Moderate|High|Critical)') {
                throw "Blocking vulnerability for $($package.packageId). Log: $path"
            }
        }
    }
    $reports += [ordered]@{
        kind = $mode.Name
        log = Get-RepositoryRelativePath $artifactRoot $path
        exitCode = $result.ExitCode
    }
}

Write-ReleaseJson ([ordered]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString("O")
    classification = @("product package", "analyzer package", "tooling only", "test only", "VSIX", "transitive", "direct")
    reports = $reports
    exceptions = $exceptions.exceptions
}) (Join-Path $artifactRoot "reports/dependency-audit.json")
