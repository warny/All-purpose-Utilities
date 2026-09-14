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
  1. If at least one other published version shares the candidate's exact major.minor.patch core and
     sorts strictly before it - regardless of whether that predecessor or the candidate itself is a
     prerelease - the baseline MUST be the most recent such version (the immediate predecessor). This
     covers every step of a release line uniformly: 2.0.0-rc.2 requires baseline 2.0.0-rc.1;
     2.0.0-rc.3 requires 2.0.0-rc.2; and - because a prerelease always sorts before the release of its
     own core version - the eventual stable 2.0.0 requires baseline 2.0.0-rc.<latest>, not an old
     stable release from a different core version such as 1.2.1. This is deliberately not "any earlier
     same-core version": once a closer baseline has been published, comparing against an older one
     could hide a real API break introduced in between.
  2. Otherwise (no other published version shares the candidate's exact core - typically the very
     first prerelease of a new line, such as 2.0.0-rc.1 itself, or the first patch/minor/major bump
     after a stable release, such as 2.0.1 following 2.0.0), the baseline must be the latest stable
     release among $PublishedVersions. This is the original, still-supported policy.
.PARAMETER CandidateVersion
The product-train candidate version (for example '2.0.0-rc.2', or eventually '2.0.0').
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

    # Not every published version is valid SemVer - some legacy releases (e.g. a stray four-part
    # "1.1.1.1") predate this repository's strict-SemVer discipline. TryParse skips those instead of
    # throwing: a version that isn't even valid SemVer can never share the candidate's exact
    # major.minor.patch core, so it is correctly excluded from the search either way.
    $sameCorePredecessors = @(
        $PublishedVersions | Where-Object { $_ -ne $CandidateVersion } | ForEach-Object {
            $parsed = $null
            if ([System.Management.Automation.SemanticVersion]::TryParse($_, [ref] $parsed)) { $parsed }
        } | Where-Object {
            $_.Major -eq $candidateSemVer.Major -and $_.Minor -eq $candidateSemVer.Minor -and $_.Patch -eq $candidateSemVer.Patch -and $_ -lt $candidateSemVer
        }
    )
    if ($sameCorePredecessors.Count -gt 0) {
        $immediatePredecessor = @($sameCorePredecessors | Sort-Object | Select-Object -Last 1)[0]
        return $baselineSemVer -eq $immediatePredecessor
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
