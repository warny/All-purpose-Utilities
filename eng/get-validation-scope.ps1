<#
.SYNOPSIS
Classifies changed repository paths for pull-request validation.
.DESCRIPTION
Returns deterministic JSON and optional Actions outputs without using a third-party action.
#>
[CmdletBinding()]
param(
    [string[]] $Paths,
    [string] $BaseRef,
    [switch] $WriteGitHubOutput
)
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $Paths) {
    if ([string]::IsNullOrWhiteSpace($BaseRef)) { throw "Paths or BaseRef is required." }
    $Paths = @(& git -C $repoRoot diff --name-only "$BaseRef...HEAD")
    if ($LASTEXITCODE -ne 0) { throw "Unable to determine changed paths." }
}
$normalized = @($Paths | ForEach-Object { $_.Replace('\', '/') } | Where-Object { $_ } | Sort-Object -Unique)
$releaseInfrastructure = @($normalized | Where-Object { $_ -like 'eng/*' -or $_ -like '.github/workflows/*' -or $_ -like 'docs/releasing/*' })
$codeOrPackages = @($normalized | Where-Object {
    $_ -match '\.(cs|csproj|props|targets)$' -or
    $_ -match '(^|/)Directory\.Build\.' -or
    $_ -in @('Directory.Packages.props', 'global.json', 'NuGet.config', 'eng/product-train-manifest.json', 'eng/release-warning-exceptions.json', 'eng/dependency-exceptions.json') -or
    $_ -like 'tests/PackagedAcceptance/*'
})
$documentationOnly = $normalized.Count -gt 0 -and @($normalized | Where-Object { $_ -notmatch '\.md$' -and $_ -notlike 'docs/*' }).Count -eq 0
$result = [ordered]@{
    paths = $normalized
    codeOrPackages = $codeOrPackages.Count -gt 0
    releaseInfrastructure = $releaseInfrastructure.Count -gt 0
    documentationOnly = $documentationOnly -and $releaseInfrastructure.Count -eq 0
    # Fail closed: only a change set proven to contain ordinary documentation can
    # bypass compilation and package validation. Unknown files always run the train.
    runProductTrain = -not $documentationOnly -or $releaseInfrastructure.Count -gt 0
    runReleaseScriptTests = $releaseInfrastructure.Count -gt 0
}
if ($WriteGitHubOutput) {
    if ([string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) { throw 'GITHUB_OUTPUT is not defined.' }
    foreach ($name in @('codeOrPackages', 'releaseInfrastructure', 'documentationOnly', 'runProductTrain', 'runReleaseScriptTests')) {
        "$name=$($result[$name].ToString().ToLowerInvariant())" | Add-Content $env:GITHUB_OUTPUT
    }
}
$result | ConvertTo-Json -Depth 4
