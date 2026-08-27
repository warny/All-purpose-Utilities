<#
.SYNOPSIS
Validates Get-PublicationDecision's partial-remote-state/resume rules without any network access.
.DESCRIPTION
Exercises the pure decision function eng/publish-product-train.ps1 uses to decide whether a
publication attempt may proceed against an observed remote package state. No dotnet nuget push,
no HTTP call, and no fake NuGet server: this only checks the decision logic itself.
#>
[CmdletBinding()] param()
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Release.Common.ps1")

function Assert-Decision {
    param(
        [string] $Case,
        [bool[]] $Exists,
        [switch] $Publish,
        [switch] $ResumePartialPublication,
        [Parameter(Mandatory)][bool] $ExpectedAllowed,
        [bool] $ExpectedResuming = $false,
        [bool] $ExpectedNothingToPublish = $false
    )
    $decision = Get-PublicationDecision -Exists $Exists -Publish:$Publish -ResumePartialPublication:$ResumePartialPublication
    if ($decision.allowed -ne $ExpectedAllowed) {
        throw "${Case}: expected allowed=$ExpectedAllowed, got $($decision.allowed) (reason: $($decision.reason))."
    }
    if ($decision.allowed -and $decision.resuming -ne $ExpectedResuming) {
        throw "${Case}: expected resuming=$ExpectedResuming, got $($decision.resuming)."
    }
    if ($decision.allowed -and $decision.nothingToPublish -ne $ExpectedNothingToPublish) {
        throw "${Case}: expected nothingToPublish=$ExpectedNothingToPublish, got $($decision.nothingToPublish)."
    }
    if (-not $decision.allowed -and [string]::IsNullOrWhiteSpace($decision.reason)) {
        throw "${Case}: a rejected decision must carry a non-empty reason."
    }
}

# 1) Empty remote state + normal publication => allowed, not a resume.
Assert-Decision "empty + Publish" -Exists @($false, $false, $false) -Publish -ExpectedAllowed $true -ExpectedResuming $false -ExpectedNothingToPublish $false

# 2) Partial remote state + normal publication (no explicit resume) => rejected.
Assert-Decision "partial + Publish (no resume)" -Exists @($true, $false, $false) -Publish -ExpectedAllowed $false

# 3) Partial remote state + explicit resume => allowed, resuming.
Assert-Decision "partial + Publish + ResumePartialPublication" -Exists @($true, $false, $false) -Publish -ResumePartialPublication -ExpectedAllowed $true -ExpectedResuming $true -ExpectedNothingToPublish $false

# 4) ResumePartialPublication without Publish => rejected, regardless of remote state.
Assert-Decision "ResumePartialPublication without Publish (empty state)" -Exists @($false, $false, $false) -ResumePartialPublication -ExpectedAllowed $false
Assert-Decision "ResumePartialPublication without Publish (partial state)" -Exists @($true, $false, $false) -ResumePartialPublication -ExpectedAllowed $false

# Additional coverage matching eng/publish-product-train.ps1's full documented behavior table.
# An empty remote state plus an explicit resume request degrades to an ordinary fresh publish:
# there is nothing yet to have interrupted a symbol-package push, so --skip-duplicate must not be
# switched on - an unexpected collision here must still be a hard, fail-closed error.
Assert-Decision "empty + Publish + ResumePartialPublication behaves like a normal publish" -Exists @($false, $false, $false) -Publish -ResumePartialPublication -ExpectedAllowed $true -ExpectedResuming $false -ExpectedNothingToPublish $false
Assert-Decision "fully present + dry run => nothing to publish, not rejected" -Exists @($true, $true, $true) -ExpectedAllowed $true -ExpectedResuming $false -ExpectedNothingToPublish $true
Assert-Decision "fully present + Publish => nothing to publish" -Exists @($true, $true, $true) -Publish -ExpectedAllowed $true -ExpectedResuming $false -ExpectedNothingToPublish $true
# Every .nupkg already existing does not mean every .snupkg made it too (the remote scan only sees
# main packages): an explicit resume against a fully-present-by-nupkg state must still retry every
# artifact, so it must not be short-circuited as "nothing to publish".
Assert-Decision "fully present (by nupkg) + Publish + ResumePartialPublication retries every artifact" -Exists @($true, $true, $true) -Publish -ResumePartialPublication -ExpectedAllowed $true -ExpectedResuming $true -ExpectedNothingToPublish $false
Assert-Decision "partial + dry run (no Publish, no Resume) => rejected" -Exists @($true, $false, $false) -ExpectedAllowed $false
Assert-Decision "partial + PreflightPackageIdsOnly-equivalent (no Publish) => rejected" -Exists @($false, $true, $false) -ExpectedAllowed $false

Write-Host "Publication decision logic tests passed."
