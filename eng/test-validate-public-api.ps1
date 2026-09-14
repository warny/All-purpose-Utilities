<#
.SYNOPSIS
Validates the API-baseline-acceptance and diagnostic-allowlist decision functions used by
validate-public-api.ps1, without invoking NuGet, ApiCompat, or any packaged product-train artifact.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"

# Dot-source the exact same shared functions validate-public-api.ps1 itself dot-sources - not a
# hand-maintained copy - so a regression in production logic cannot leave this test green by
# accident.
. (Join-Path $PSScriptRoot "ApiCompat.Common.ps1")

function Assert-True { param([bool]$Condition, [string]$Message) if (-not $Condition) { throw $Message } }
function Assert-False { param([bool]$Condition, [string]$Message) if ($Condition) { throw $Message } }

# ---------------------------------------------------------------------------------------------
# Scenario: a prerelease baseline that exists on NuGet, shares the candidate's exact core version,
# and precedes it, is accepted (the RC1-as-RC2-baseline policy this PR introduces).
# ---------------------------------------------------------------------------------------------
Assert-True (Test-ApiBaselineVersion -CandidateVersion '2.0.0-rc.2' -BaselineVersion '2.0.0-rc.1' -PublishedVersions @('2.0.0-rc.1')) `
    "RC1 baseline for RC2 candidate must be accepted."

# ---------------------------------------------------------------------------------------------
# Scenario: the latest-stable policy (the original, pre-existing behavior) still works when no
# same-line prerelease has been published yet (the very first prerelease of a line, e.g. rc.1).
# ---------------------------------------------------------------------------------------------
Assert-True (Test-ApiBaselineVersion -CandidateVersion '2.0.0-rc.1' -BaselineVersion '1.2.1' -PublishedVersions @('1.0.0', '1.2.0', '1.2.1')) `
    "Latest-stable baseline must still be accepted for the first prerelease of a line."

# ---------------------------------------------------------------------------------------------
# Scenario (regression - human review finding): once a same-line prerelease has been published,
# it takes PRIORITY over the latest stable release, even if the latest stable release is also
# technically a valid version string. A stale/mistaken 'publishedVersion: 1.2.1' must NOT be
# silently accepted for an RC2 candidate once 2.0.0-rc.1 exists - that could hide a real API break
# introduced between RC1 and RC2 that a 1.2.1 comparison would never see.
# ---------------------------------------------------------------------------------------------
Assert-False (Test-ApiBaselineVersion -CandidateVersion '2.0.0-rc.2' -BaselineVersion '1.2.1' -PublishedVersions @('1.0.0', '1.2.0', '1.2.1', '2.0.0-rc.1')) `
    "A stale stable baseline must be rejected once a same-line prerelease predecessor has been published."

# ---------------------------------------------------------------------------------------------
# Scenario (regression - human review finding): the baseline must be the IMMEDIATE predecessor,
# not merely any earlier same-line prerelease. Candidate rc.3 with rc.1 and rc.2 both published
# must require rc.2, not rc.1 (skipping rc.2 could hide a break introduced in rc.2).
# ---------------------------------------------------------------------------------------------
Assert-False (Test-ApiBaselineVersion -CandidateVersion '2.0.0-rc.3' -BaselineVersion '2.0.0-rc.1' -PublishedVersions @('2.0.0-rc.1', '2.0.0-rc.2', '2.0.0-rc.3')) `
    "A baseline older than the immediate predecessor prerelease must be rejected."
Assert-True (Test-ApiBaselineVersion -CandidateVersion '2.0.0-rc.3' -BaselineVersion '2.0.0-rc.2' -PublishedVersions @('2.0.0-rc.1', '2.0.0-rc.2', '2.0.0-rc.3')) `
    "The immediate predecessor prerelease (rc.2 for candidate rc.3) must be accepted."

# ---------------------------------------------------------------------------------------------
# Scenario: numeric (not lexical) prerelease ordering - rc.2 must sort before rc.10.
# ---------------------------------------------------------------------------------------------
Assert-True (Test-ApiBaselineVersion -CandidateVersion '2.0.0-rc.10' -BaselineVersion '2.0.0-rc.9' -PublishedVersions @('2.0.0-rc.1', '2.0.0-rc.2', '2.0.0-rc.9', '2.0.0-rc.10')) `
    "Prerelease ordering must be numeric: rc.9 is the immediate predecessor of rc.10, not rc.2."

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
# Scenario (regression - human review finding): the immediate-predecessor rule applies uniformly
# across the RC-to-stable transition too, not only RC-to-RC. Once 2.0.0-rc.1 and 2.0.0-rc.2 have
# been published, the eventual stable 2.0.0 candidate must require baseline 2.0.0-rc.2 (a
# prerelease always sorts before the release of its own core version) - NOT an old, unrelated
# stable release such as 1.2.1, which would miss any API break introduced across the RC series.
# ---------------------------------------------------------------------------------------------
Assert-True (Test-ApiBaselineVersion -CandidateVersion '2.0.0' -BaselineVersion '2.0.0-rc.2' -PublishedVersions @('1.2.1', '2.0.0-rc.1', '2.0.0-rc.2')) `
    "A stable candidate must require its own line's latest prerelease as baseline, once one has been published."
Assert-False (Test-ApiBaselineVersion -CandidateVersion '2.0.0' -BaselineVersion '1.2.1' -PublishedVersions @('1.2.1', '2.0.0-rc.1', '2.0.0-rc.2')) `
    "A stable candidate must not fall back to an old, unrelated stable release once its own line's prerelease exists."

# ---------------------------------------------------------------------------------------------
# Scenario: once a core version has actually shipped stable, the NEXT core version (a new patch/
# minor/major with no prereleases of its own published yet) correctly falls back to the latest
# stable release - here the 2.0.0 that was just used as a baseline candidate above.
# ---------------------------------------------------------------------------------------------
Assert-True (Test-ApiBaselineVersion -CandidateVersion '2.0.1' -BaselineVersion '2.0.0' -PublishedVersions @('1.2.1', '2.0.0-rc.1', '2.0.0-rc.2', '2.0.0')) `
    "The first candidate of a new core version must fall back to the latest stable release."

# ---------------------------------------------------------------------------------------------
# Scenario (regression - found via the real end-to-end pipeline run, not by this test file
# alone): several real packages in this repository have a legacy four-part published version
# (e.g. '1.1.1.1'), which is not valid SemVer. Searching every published version for a same-core
# predecessor must tolerate that instead of throwing - a version that cannot even be parsed as
# SemVer can never share the candidate's exact three-part core anyway.
# ---------------------------------------------------------------------------------------------
Assert-True (Test-ApiBaselineVersion -CandidateVersion '2.0.0-rc.1' -BaselineVersion '1.2.1' -PublishedVersions @('1.0.0', '1.1.1.1', '1.2.0', '1.2.1')) `
    "A published version list containing a non-SemVer legacy entry must not throw, and must still resolve the correct latest-stable baseline."

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
