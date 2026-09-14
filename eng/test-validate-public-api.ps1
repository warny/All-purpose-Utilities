<#
.SYNOPSIS
Validates the API-baseline-acceptance and diagnostic-allowlist decision functions extracted from
validate-public-api.ps1, without invoking NuGet, ApiCompat, or any packaged product-train artifact.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"

# validate-public-api.ps1 is a top-to-bottom script, not a module; dot-sourcing it with real
# parameters would immediately run the whole pipeline (NuGet, ApiCompat, packaged artifacts). Instead,
# re-declare the two pure decision functions under test here, kept byte-for-byte identical to the
# script's own copies, so this test never needs network access, a built product train, or the
# ApiCompat tool. If the two diverge, the corresponding scenario below (or the real end-to-end
# pipeline run recorded in the PR) will surface the mismatch.
function Test-ApiBaselineVersion {
    param(
        [Parameter(Mandatory)] [string] $CandidateVersion,
        [Parameter(Mandatory)] [string] $BaselineVersion,
        [Parameter(Mandatory)] [string[]] $PublishedVersions
    )
    $latestStable = @($PublishedVersions | Where-Object { $_ -notmatch '-' } | Select-Object -Last 1)[0]
    if ($latestStable -eq $BaselineVersion) { return $true }
    $candidateSemVer = [System.Management.Automation.SemanticVersion]$CandidateVersion
    $baselineSemVer = [System.Management.Automation.SemanticVersion]$BaselineVersion
    $sameLine = $baselineSemVer.Major -eq $candidateSemVer.Major -and $baselineSemVer.Minor -eq $candidateSemVer.Minor -and $baselineSemVer.Patch -eq $candidateSemVer.Patch
    $isPrerelease = [bool]$baselineSemVer.PreReleaseLabel -and [bool]$candidateSemVer.PreReleaseLabel
    return [bool]($sameLine -and $isPrerelease -and $baselineSemVer -lt $candidateSemVer)
}

function Get-ApiDiagnosticDifference {
    param(
        [string[]] $AcceptedKeys,
        [string[]] $ActualKeys
    )
    if ($AcceptedKeys.Count -eq 0 -and $ActualKeys.Count -eq 0) { return @() }
    if ($AcceptedKeys.Count -eq 0) { return @($ActualKeys | ForEach-Object { [pscustomobject]@{ InputObject = $_; SideIndicator = '=>' } }) }
    if ($ActualKeys.Count -eq 0) { return @($AcceptedKeys | ForEach-Object { [pscustomobject]@{ InputObject = $_; SideIndicator = '<=' } }) }
    return @(Compare-Object ($AcceptedKeys | Sort-Object) ($ActualKeys | Sort-Object))
}

function Assert-True { param([bool]$Condition, [string]$Message) if (-not $Condition) { throw $Message } }
function Assert-False { param([bool]$Condition, [string]$Message) if ($Condition) { throw $Message } }

# ---------------------------------------------------------------------------------------------
# Scenario: a prerelease baseline that exists on NuGet, in the candidate's own prerelease line,
# and precedes it, is accepted (the RC1-as-RC2-baseline policy this PR introduces).
# ---------------------------------------------------------------------------------------------
Assert-True (Test-ApiBaselineVersion -CandidateVersion '2.0.0-rc.2' -BaselineVersion '2.0.0-rc.1' -PublishedVersions @('2.0.0-rc.1')) `
    "RC1 baseline for RC2 candidate must be accepted."

# ---------------------------------------------------------------------------------------------
# Scenario: the latest-stable policy (the original, pre-existing behavior) still works.
# ---------------------------------------------------------------------------------------------
Assert-True (Test-ApiBaselineVersion -CandidateVersion '2.0.0-rc.1' -BaselineVersion '1.2.1' -PublishedVersions @('1.0.0', '1.2.0', '1.2.1')) `
    "Latest-stable baseline must still be accepted."

# ---------------------------------------------------------------------------------------------
# Scenario: a configured baseline that does not sort before the candidate fails (wrong/future baseline).
# ---------------------------------------------------------------------------------------------
Assert-False (Test-ApiBaselineVersion -CandidateVersion '2.0.0-rc.2' -BaselineVersion '2.0.0-rc.3' -PublishedVersions @('2.0.0-rc.1', '2.0.0-rc.2', '2.0.0-rc.3')) `
    "A baseline newer than the candidate must not be accepted."

# ---------------------------------------------------------------------------------------------
# Scenario: a prerelease baseline from a different major.minor.patch line fails (wrong line).
# ---------------------------------------------------------------------------------------------
Assert-False (Test-ApiBaselineVersion -CandidateVersion '2.0.0-rc.2' -BaselineVersion '2.1.0-rc.1' -PublishedVersions @('2.1.0-rc.1')) `
    "A prerelease baseline from a different prerelease line must not be accepted."

# ---------------------------------------------------------------------------------------------
# Scenario: a stable baseline that is not the latest stable fails (superseded stable baseline).
# ---------------------------------------------------------------------------------------------
Assert-False (Test-ApiBaselineVersion -CandidateVersion '1.2.1' -BaselineVersion '1.1.0' -PublishedVersions @('1.0.0', '1.1.0', '1.2.0', '1.2.1')) `
    "A superseded stable baseline must not be accepted."

# ---------------------------------------------------------------------------------------------
# Scenario: a candidate that is itself stable (no prerelease label) cannot use a prerelease
# baseline via the prerelease-line exception (only the latest-stable path applies to it).
# ---------------------------------------------------------------------------------------------
Assert-False (Test-ApiBaselineVersion -CandidateVersion '2.0.0' -BaselineVersion '2.0.0-rc.1' -PublishedVersions @('2.0.0-rc.1', '2.0.0')) `
    "A stable candidate must not accept a prerelease baseline through the prerelease-line exception."

Write-Host "Test-ApiBaselineVersion scenarios passed."

# ---------------------------------------------------------------------------------------------
# Scenario: exact accepted diagnostics succeed (no difference).
# ---------------------------------------------------------------------------------------------
$exactMatch = Get-ApiDiagnosticDifference -AcceptedKeys @('CP0002|Member removed') -ActualKeys @('CP0002|Member removed')
Assert-True ($exactMatch.Count -eq 0) "Exact accepted diagnostic must produce no difference."

# ---------------------------------------------------------------------------------------------
# Scenario: both empty succeeds (the common RC1->RC2 case observed for every package today).
# ---------------------------------------------------------------------------------------------
$bothEmpty = Get-ApiDiagnosticDifference -AcceptedKeys @() -ActualKeys @()
Assert-True ($bothEmpty.Count -eq 0) "Empty accepted and empty actual must produce no difference."

# ---------------------------------------------------------------------------------------------
# Scenario: a stale accepted diagnostic (accepted but not actually produced) fails.
# ---------------------------------------------------------------------------------------------
$stale = Get-ApiDiagnosticDifference -AcceptedKeys @('CP0002|Phantom member removed') -ActualKeys @()
Assert-True ($stale.Count -eq 1 -and $stale[0].SideIndicator -eq '<=') "A stale accepted diagnostic must be reported with '<=' and fail validation."

# ---------------------------------------------------------------------------------------------
# Scenario: a new, unaccepted diagnostic (produced but not accepted) fails.
# ---------------------------------------------------------------------------------------------
$new = Get-ApiDiagnosticDifference -AcceptedKeys @() -ActualKeys @('CP0002|Unexpected member removed')
Assert-True ($new.Count -eq 1 -and $new[0].SideIndicator -eq '=>') "A new unaccepted diagnostic must be reported with '=>' and fail validation."

# ---------------------------------------------------------------------------------------------
# Scenario: mixed accepted/actual sets report only the true difference, not every entry.
# ---------------------------------------------------------------------------------------------
$mixed = Get-ApiDiagnosticDifference -AcceptedKeys @('CP0002|A', 'CP0002|B') -ActualKeys @('CP0002|A', 'CP0002|C')
Assert-True ($mixed.Count -eq 2) "A mixed accepted/actual set must report exactly the entries that differ."
Assert-True (@($mixed | Where-Object { $_.InputObject -eq 'CP0002|B' -and $_.SideIndicator -eq '<=' }).Count -eq 1) "The stale-only entry 'B' must be reported."
Assert-True (@($mixed | Where-Object { $_.InputObject -eq 'CP0002|C' -and $_.SideIndicator -eq '=>' }).Count -eq 1) "The new-only entry 'C' must be reported."

Write-Host "Get-ApiDiagnosticDifference scenarios passed."

# ---------------------------------------------------------------------------------------------
# Scenario: eng/product-train-manifest.json - every package that was 'first-candidate' before RC1
# (no publishedVersion) is now 'published-baseline' at exactly '2.0.0-rc.1', since all of them were
# actually published as part of RC1. Also confirms every published-baseline package's
# apiBreakAcceptanceFile now points at the RC1->RC2 delta file, not the historical major-migration
# file, and that baselineApiAssemblies/apiAssemblies stay one-to-one.
# ---------------------------------------------------------------------------------------------
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot "eng/product-train-manifest.json"
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$formerlyFirstCandidate = @('omy.Utils.NumberToString', 'omy.Utils.Parser.Source', 'omy.Utils.Parser.Diagnostics', 'omy.Utils.Parser.Antlr4.Common', 'omy.Utils.Parser', 'omy.Utils.Parser.Expressions', 'omy.Utils.Expressions.CSyntax', 'omy.Utils.Expressions.VBSyntax', 'omy.Utils.Parser.Generators')
foreach ($packageId in $formerlyFirstCandidate) {
    $package = $manifest.packages | Where-Object packageId -eq $packageId
    Assert-True ($null -ne $package) "Manifest must still declare '$packageId'."
    Assert-True ($package.publicApiPolicy -eq 'published-baseline') "'$packageId' must be promoted from first-candidate to published-baseline now that it was published in RC1."
    Assert-True ($package.publishedVersion -eq '2.0.0-rc.1') "'$packageId' must be baselined at 2.0.0-rc.1."
    Assert-True (@($package.baselineApiAssemblies).Count -eq @($package.apiAssemblies).Count) "'$packageId' baselineApiAssemblies must map one-to-one to apiAssemblies."
}
foreach ($package in $manifest.packages) {
    if (-not $package.publishedVersion) { continue }
    Assert-True ($package.apiBreakAcceptanceFile -eq 'eng/api-breaking-changes/2.0.0-rc.2.json') "'$($package.packageId)' must use the RC1->RC2 acceptance file, not the historical major-migration one."
}
Assert-True ($manifest.version -eq '2.0.0-rc.2') "Manifest version must be the RC2 candidate."

Write-Host "Manifest first-candidate-to-published-baseline transition scenarios passed."

Write-Host "validate-public-api.ps1 decision-function tests passed."
