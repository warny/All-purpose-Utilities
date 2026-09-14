<#
.SYNOPSIS
Shared, pure (no network/filesystem) decision functions for the API-baseline-acceptance and
diagnostic-allowlist logic used by validate-public-api.ps1. Dot-sourced by both that script and
eng/test-validate-public-api.ps1, so the test exercises the exact same code path production runs -
not a hand-maintained copy that could silently drift from it.
#>

<#
.SYNOPSIS
Decides whether a configured, already-confirmed-to-exist API baseline version is an acceptable
comparison point for a candidate version.
.DESCRIPTION
Accepted in exactly two shapes, tried in this priority order:
  1. If the candidate is itself a prerelease and at least one earlier prerelease within the exact
     same major.minor.patch line has already been published, the baseline MUST be the most recent
     such prerelease (the immediate predecessor) - for example candidate 2.0.0-rc.3 requires baseline
     2.0.0-rc.2, not 2.0.0-rc.1 and not the last stable release. This is deliberately not "any earlier
     prerelease": once a closer baseline has been published, comparing against an older one (or an
     already-superseded stable release) could hide a real API break introduced in between.
  2. Otherwise (no earlier same-line prerelease exists yet - typically the very first prerelease of a
     line, such as 2.0.0-rc.1 itself - or the candidate is itself stable), the baseline must be the
     latest stable release among $PublishedVersions. This is the original, still-supported policy.
.PARAMETER CandidateVersion
The product-train candidate version (for example '2.0.0-rc.2').
.PARAMETER BaselineVersion
The package's configured baseline version (for example '2.0.0-rc.1' or '1.2.1').
.PARAMETER PublishedVersions
Every version string published for the package on NuGet.
#>
function Test-ApiBaselineVersion {
    param(
        [Parameter(Mandatory)] [string] $CandidateVersion,
        [Parameter(Mandatory)] [string] $BaselineVersion,
        [Parameter(Mandatory)] [string[]] $PublishedVersions
    )
    $candidateSemVer = [System.Management.Automation.SemanticVersion]$CandidateVersion
    $baselineSemVer = [System.Management.Automation.SemanticVersion]$BaselineVersion

    if ($candidateSemVer.PreReleaseLabel) {
        $sameLinePrereleases = @(
            $PublishedVersions | Where-Object { $_ -match '-' } | ForEach-Object { [System.Management.Automation.SemanticVersion]$_ } | Where-Object {
                $_.Major -eq $candidateSemVer.Major -and $_.Minor -eq $candidateSemVer.Minor -and $_.Patch -eq $candidateSemVer.Patch -and $_ -lt $candidateSemVer
            }
        )
        if ($sameLinePrereleases.Count -gt 0) {
            $immediatePredecessor = @($sameLinePrereleases | Sort-Object | Select-Object -Last 1)[0]
            return $baselineSemVer -eq $immediatePredecessor
        }
    }

    $latestStable = @($PublishedVersions | Where-Object { $_ -notmatch '-' } | Select-Object -Last 1)[0]
    return $latestStable -eq $BaselineVersion
}

<#
.SYNOPSIS
Computes the exact-match difference between an accepted API-diagnostic allowlist and the diagnostics
actually observed, mirroring Compare-Object's SideIndicator convention ('<=' stale accepted entry with
no matching actual diagnostic, '=>' new actual diagnostic with no matching accepted entry) without
Compare-Object's inability to accept two empty arrays directly.
.PARAMETER AcceptedKeys
The "diagnosticId|normalizedMessage" keys explicitly accepted in the allowlist.
.PARAMETER ActualKeys
The "diagnosticId|normalizedMessage" keys ApiCompat actually produced.
#>
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
