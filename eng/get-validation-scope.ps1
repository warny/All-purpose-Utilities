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
    [switch] $ForceProductTrain,
    [switch] $WriteGitHubOutput
)
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
if ($ForceProductTrain) {
    $Paths = @('manual-full-validation')
}
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
$documentationOnly = $normalized.Count -gt 0 -and @($normalized | Where-Object {
    # Root README and the documentation tree are the only proven non-product inputs.
    # A README beside a project may be packed, so it deliberately runs the train.
    $_ -ne 'README.md' -and $_ -notlike 'docs/*'
}).Count -eq 0
$vsixPaths = @($normalized | Where-Object {
    $_ -like 'Utils.Parser.VisualStudio/*' -or
    $_ -like 'Utils.Parser.VisualStudio.Worker/*' -or
    $_ -eq 'res/AllPurposeUtilities_logo.png' -or
    $_ -eq 'eng/test-vsix-package.ps1' -or
    $_ -eq 'Directory.Build.props'
})
$result = [ordered]@{
    paths = $normalized
    codeOrPackages = $codeOrPackages.Count -gt 0
    releaseInfrastructure = $releaseInfrastructure.Count -gt 0
    documentationOnly = $documentationOnly -and $releaseInfrastructure.Count -eq 0
    # Fail closed: only a change set proven to contain ordinary documentation can
    # bypass compilation and package validation. Unknown files always run the train.
    runProductTrain = -not $documentationOnly -or $releaseInfrastructure.Count -gt 0
    runReleaseScriptTests = $releaseInfrastructure.Count -gt 0
    # The VSIX build+validation gate is Windows-only and comparatively slow; run it only
    # when a change could plausibly affect the extension, its worker, its packaging, or the
    # gate script itself (or when explicitly forced, e.g. workflow_dispatch).
    runVsix = $vsixPaths.Count -gt 0 -or $ForceProductTrain.IsPresent
}
if ($WriteGitHubOutput) {
    if ([string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) { throw 'GITHUB_OUTPUT is not defined.' }
    foreach ($name in @('codeOrPackages', 'releaseInfrastructure', 'documentationOnly', 'runProductTrain', 'runReleaseScriptTests', 'runVsix')) {
        "$name=$($result[$name].ToString().ToLowerInvariant())" | Add-Content $env:GITHUB_OUTPUT
    }
}
$result | ConvertTo-Json -Depth 4
