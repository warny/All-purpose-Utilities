<#
.SYNOPSIS
Preflights and optionally publishes an immutable, validated omy product train.
.DESCRIPTION
Normal dry-runs and publication require the candidate manifest produced by the release gates.
PreflightPackageIdsOnly is the sole artifact-free mode and only checks remote availability.
#>
[CmdletBinding()]
param(
    [string] $ArtifactsPath = "artifacts",
    [switch] $Publish,
    [switch] $PreflightPackageIdsOnly,
    [switch] $ValidateCandidateOnly,
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
if (-not $Publish -and $present.Count -ne 0 -and $present.Count -ne $states.Count) {
    # A dry run or ID-only preflight has no way to push anything, so a partial remote state here
    # can only mean an out-of-band or corrupted train - it must not be silently reported as fine.
    # An actual -Publish run may legitimately see this exact state after resuming a prior partial
    # publication; see the resume handling below.
    throw "Incoherent remote product-train state; no package was pushed: $(($states | ForEach-Object { "$($_.packageId)=$($_.exists)" }) -join ', ')."
}
$state = if ($present.Count -eq $states.Count) { "fully-published" } elseif ($present.Count -eq 0) { "fully-available" } else { "partially-published" }
$plan = [ordered]@{ generatedAtUtc = [DateTime]::UtcNow.ToString('O'); productTrain = [string]$manifest.productTrain; version = [string]$manifest.version; state = $state; packageIdsOnly = [bool]$PreflightPackageIdsOnly; publicationEnabled = [bool]$Publish; order = $publicationOrder; packages = $states; published = @($present.packageId); notPublished = @($states | Where-Object { -not $_.exists } | ForEach-Object packageId); nextManualAction = "Publish only the immutable candidate artifacts after explicit approval." }
$reportRoot = Join-Path $artifactRoot "reports"
New-Item $reportRoot -ItemType Directory -Force | Out-Null
$plan | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $reportRoot "publication-plan.json")
if ($present.Count -eq $states.Count) { Write-Host "All packages already exist; nothing to publish."; return }
if (-not $Publish) { Write-Host "Dry run passed: all package IDs are available at $($manifest.version)."; return }
if ([string]::IsNullOrWhiteSpace($ApiKey)) { throw "An API key is required for publication." }
if ($present.Count -gt 0) {
    Write-Host "Resuming publication: $($present.Count) of $($states.Count) packages already exist at $($manifest.version) and will be skipped."
}
$presentIds = @($present.packageId)
$candidateById = @{}; $candidate.packages | ForEach-Object { $candidateById[$_.packageId] = $_ }
foreach ($packageId in $publicationOrder) {
    if ($presentIds -contains $packageId) {
        Write-Host "Skip: $packageId $($manifest.version) already exists on NuGet."
        continue
    }
    # Push only the .nupkg: `dotnet nuget push` publishes the matching .snupkg from the same
    # directory automatically unless -n/--no-symbols is passed. Pushing both explicitly would
    # push every symbol package twice - the second push then fails or conflicts with the first.
    # --skip-duplicate turns a 409 (this exact immutable id+version already published - NuGet
    # never allows different content under an existing version) into a warning instead of a hard
    # failure, so a transient failure earlier in this same loop can be safely retried by rerunning
    # this command; it does not skip a package this run has not attempted yet.
    $nupkg = $candidateById[$packageId].nupkg
    & dotnet nuget push (Join-Path $packagesPath $nupkg.file) --api-key $ApiKey --source "https://api.nuget.org/v3/index.json" --skip-duplicate
    if ($LASTEXITCODE -ne 0) { throw "Publication stopped after failure pushing '$($nupkg.file)'." }
}
