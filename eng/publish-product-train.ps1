<#
.SYNOPSIS
Preflights and optionally publishes an immutable, validated omy product train.
.DESCRIPTION
Normal dry-runs and publication require the candidate manifest produced by the release gates.
PreflightPackageIdsOnly is the sole artifact-free mode and only checks remote availability.

A remote state where some but not all manifested packages already exist at the candidate version
is never auto-accepted, by any mode, as "this must be a resume": -Publish -ResumePartialPublication
is the only combination that proceeds past it, and it is meant to be typed deliberately by a human
who already knows a previous publication attempt of this exact candidate was interrupted. Every
other combination (a dry run, -PreflightPackageIdsOnly, or a plain -Publish) rejects that same
partial state outright. NuGet guarantees a published id+version's content is immutable, so resuming
never needs to download and hash-compare already-published packages: -ResumePartialPublication just
adds --skip-duplicate to every push, which lets NuGet turn a re-push of an already-published,
immutable artifact into a no-op instead of an error, while a genuinely missing artifact (for example
a .snupkg whose earlier push failed after its .nupkg succeeded) still gets a real attempt.
#>
[CmdletBinding()]
param(
    [string] $ArtifactsPath = "artifacts",
    [switch] $Publish,
    [switch] $PreflightPackageIdsOnly,
    [switch] $ValidateCandidateOnly,
    [switch] $ResumePartialPublication,
    [string] $ApiKey = $env:NUGET_API_KEY,
    [string] $CandidateManifestPath
)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Release.Common.ps1")
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content (Join-Path $PSScriptRoot "product-train-manifest.json") -Raw | ConvertFrom-Json
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $ArtifactsPath))
$packagesPath = Join-Path $artifactRoot "packages"
if ($Publish -and $PreflightPackageIdsOnly) { throw "PreflightPackageIdsOnly can never publish packages." }
if ($ValidateCandidateOnly -and ($Publish -or $PreflightPackageIdsOnly)) { throw "ValidateCandidateOnly cannot publish or run an artifact-free preflight." }
if ($ResumePartialPublication -and -not $Publish) { throw "-ResumePartialPublication requires -Publish; it only ever applies to an explicit, human-initiated resume of a real publication attempt." }
if ([string]::IsNullOrWhiteSpace($CandidateManifestPath)) {
    $CandidateManifestPath = Join-Path $artifactRoot "manifests/release-candidate-manifest.json"
}

$expectedIds = @($manifest.packages.packageId)
$candidate = $null
$publicationOrder = @()
if (-not $PreflightPackageIdsOnly) {
    if (-not (Test-Path -LiteralPath $CandidateManifestPath)) {
        throw "A validated candidate manifest is required for publication planning and publication. Use -PreflightPackageIdsOnly only to check package ID availability."
    }
    $candidate = Get-Content $CandidateManifestPath -Raw | ConvertFrom-Json
    if (-not $ValidateCandidateOnly -and ($candidate.validationTier -ne 'full-release' -or -not $candidate.reproducibilityValidated)) { throw 'Publication requires a full-release candidate with validated reproducibility.' }
    $commit = (& git -C $repoRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Unable to resolve the repository commit." }
    if ($candidate.productTrain -ne $manifest.productTrain -or $candidate.version -ne $manifest.version -or $candidate.repository -ne $manifest.repository -or $candidate.commit -ne $commit) {
        throw "Candidate manifest identity, version, repository, or commit mismatch."
    }
    $candidateIds = @($candidate.packages.packageId)
    if (($candidateIds | Sort-Object -Unique).Count -ne $expectedIds.Count -or (Compare-Object ($expectedIds | Sort-Object) ($candidateIds | Sort-Object))) {
        throw "Candidate manifest package list must contain every source-manifest package exactly once and no unknown package."
    }
    $publicationOrder = @($candidate.publicationOrder)
    if (($publicationOrder | Sort-Object -Unique).Count -ne $expectedIds.Count -or (Compare-Object ($expectedIds | Sort-Object) ($publicationOrder | Sort-Object))) {
        throw "Candidate publicationOrder must contain every package exactly once and no unknown package."
    }
    $listedFiles = @()
    foreach ($item in $candidate.packages) {
        $expectedItemVersion = [string]$manifest.version
        if ($item.version -ne $expectedItemVersion -or $item.acceptanceResult -ne 'passed' -or $item.warningsResult -ne 'passed' -or $item.sourceLinkResult -ne 'passed') {
            throw "$($item.packageId): one or more mandatory candidate gates did not pass."
        }
        if ($item.apiCompatibilityResult -notin @('compatible', 'accepted-major-version-breaks', 'baseline-created')) {
            throw "$($item.packageId): API compatibility gate did not pass."
        }
        if ($candidate.reproducibilityValidated -and (-not @($item.reproducibilityResult) -or @($item.reproducibilityResult | Where-Object { $_ -notin @('bit-identical', 'logically-identical-after-zip-normalization') }))) {
            throw "$($item.packageId): reproducibility gate did not pass."
        }
        foreach ($artifact in @($item.nupkg, $item.snupkg)) {
            if ($null -eq $artifact -or [string]::IsNullOrWhiteSpace($artifact.file) -or [string]::IsNullOrWhiteSpace($artifact.sha256)) {
                throw "$($item.packageId): both nupkg and snupkg must be listed with hashes."
            }
            $listedFiles += [string]$artifact.file
            $path = Join-Path $packagesPath $artifact.file
            if (-not (Test-Path -LiteralPath $path) -or (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $artifact.sha256) {
                throw "$($item.packageId): validated artifact hash mismatch for '$($artifact.file)'."
            }
        }
    }
    $actualFiles = @(Get-ChildItem $packagesPath -File | Where-Object Extension -in @('.nupkg', '.snupkg') | Select-Object -ExpandProperty Name)
    if (Compare-Object ($listedFiles | Sort-Object) ($actualFiles | Sort-Object)) { throw "Package directory differs from the exact candidate artifact list." }
}
if ($ValidateCandidateOnly) { Write-Host "Validated candidate manifest and package hashes without contacting NuGet."; return }

$states = foreach ($package in $manifest.packages) {
    $id = [string]$package.packageId
    $expectedVersion = [string]$manifest.version
    $url = "https://api.nuget.org/v3-flatcontainer/$($id.ToLowerInvariant())/index.json"
    try { $exists = @((Invoke-RestMethod -Uri $url -ErrorAction Stop).versions) -contains $expectedVersion }
    catch { if ($_.Exception.Response.StatusCode.value__ -eq 404) { $exists = $false } else { throw } }
    [pscustomobject]@{ packageId = $id; version = $expectedVersion; exists = $exists }
}
$present = @($states | Where-Object exists)
# A partial remote state is never auto-accepted as "this must be a resume" - Get-PublicationDecision
# is the single source of truth for that rule (see Release.Common.ps1) and is exercised directly by
# eng/test-publish-decision.ps1 without any network access.
$decision = Get-PublicationDecision -Exists @($states.exists) -Publish:$Publish -ResumePartialPublication:$ResumePartialPublication
if (-not $decision.allowed) {
    throw "Incoherent remote product-train state; no package was pushed: $(($states | ForEach-Object { "$($_.packageId)=$($_.exists)" }) -join ', '). $($decision.reason) If this is a known, previously-interrupted publication of this exact candidate, rerun with -Publish -ResumePartialPublication to resume it explicitly."
}
$resuming = $decision.resuming
$state = if ($present.Count -eq $states.Count) { "fully-published" } elseif ($present.Count -eq 0) { "fully-available" } else { "partially-published" }
$plan = [ordered]@{ generatedAtUtc = [DateTime]::UtcNow.ToString('O'); productTrain = [string]$manifest.productTrain; version = [string]$manifest.version; state = $state; packageIdsOnly = [bool]$PreflightPackageIdsOnly; publicationEnabled = [bool]$Publish; resumePartialPublication = [bool]$ResumePartialPublication; order = $publicationOrder; packages = $states; published = @($present.packageId); notPublished = @($states | Where-Object { -not $_.exists } | ForEach-Object packageId); nextManualAction = "Publish only the immutable candidate artifacts after explicit approval." }
$reportRoot = Join-Path $artifactRoot "reports"
New-Item $reportRoot -ItemType Directory -Force | Out-Null
$plan | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $reportRoot "publication-plan.json")
if ($decision.nothingToPublish) { Write-Host "All packages already exist; nothing to publish."; return }
if (-not $Publish) { Write-Host "Dry run passed: all package IDs are available at $($manifest.version)."; return }
if ([string]::IsNullOrWhiteSpace($ApiKey)) { throw "An API key is required for publication." }
if ($resuming) {
    Write-Host "Resuming publication: $($present.Count) of $($states.Count) packages already exist at $($manifest.version). Every artifact is still attempted with --skip-duplicate, so a package whose .nupkg published but whose .snupkg failed is completed rather than skipped entirely."
}
$candidateById = @{}; $candidate.packages | ForEach-Object { $candidateById[$_.packageId] = $_ }
$skipDuplicateArgs = if ($resuming) { @('--skip-duplicate') } else { @() }
foreach ($packageId in $publicationOrder) {
    # Never decide, from remote state alone, to skip a package's push attempts entirely: the
    # remote-state scan above only decides whether a partial state is allowed to proceed (the
    # -ResumePartialPublication gate), not which artifacts get attempted here. Every artifact for
    # every package in the candidate is always pushed; in resume mode --skip-duplicate lets NuGet
    # itself turn an already-published, immutable artifact into a no-op warning instead of an
    # error, while a genuinely missing artifact (for example a .snupkg whose earlier push failed)
    # still gets a real attempt. Outside resume mode a 409 here is a real, unexpected error and is
    # not swallowed, keeping a fresh publication fail-closed.
    $item = $candidateById[$packageId]

    # nupkg first, explicitly without its automatically-pushed symbol package: pushing both
    # unconditionally (nupkg with symbols, then snupkg again) would push every .snupkg twice.
    & dotnet nuget push (Join-Path $packagesPath $item.nupkg.file) --no-symbols --api-key $ApiKey --source "https://api.nuget.org/v3/index.json" @skipDuplicateArgs
    if ($LASTEXITCODE -ne 0) { throw "Publication stopped after failure pushing '$($item.nupkg.file)'." }

    # snupkg second and separately, so a resume can complete it even when the nupkg above was a
    # --skip-duplicate no-op because a prior attempt already published it.
    & dotnet nuget push (Join-Path $packagesPath $item.snupkg.file) --api-key $ApiKey --source "https://api.nuget.org/v3/index.json" @skipDuplicateArgs
    if ($LASTEXITCODE -ne 0) { throw "Publication stopped after failure pushing '$($item.snupkg.file)'." }
}
