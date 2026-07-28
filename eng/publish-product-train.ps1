<#
.SYNOPSIS
Preflights and optionally publishes one synchronized omy product train.
.DESCRIPTION
Checks all manifested package IDs before the first push. A fully absent train may be published in
manifest order; a fully present train is a no-op; a partially present train always fails.
#>
[CmdletBinding()]
param(
    [string] $ArtifactsPath = "artifacts",
    [switch] $Publish,
    [string] $ApiKey = $env:NUGET_API_KEY,
    [string] $CandidateManifestPath
)
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content (Join-Path $PSScriptRoot "product-train-manifest.json") -Raw | ConvertFrom-Json
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $ArtifactsPath))
$packagesPath = Join-Path $artifactRoot "packages"
if ([string]::IsNullOrWhiteSpace($CandidateManifestPath)) { $CandidateManifestPath = Join-Path $artifactRoot "manifests/release-candidate-manifest.json" }
if (Test-Path $CandidateManifestPath) {
    $candidate = Get-Content $CandidateManifestPath -Raw | ConvertFrom-Json
    if ($candidate.version -ne $manifest.version -or $candidate.commit -ne (& git -C $repoRoot rev-parse HEAD).Trim()) { throw "Candidate manifest version or commit mismatch." }
    if (($candidate.packages.packageId -join ',') -ne ($manifest.packages.packageId -join ',')) { throw "Candidate manifest package list differs from source manifest." }
    foreach ($item in $candidate.packages) { foreach ($artifact in @($item.nupkg, $item.snupkg)) { $path=Join-Path $packagesPath $artifact.file; if (-not (Test-Path $path) -or (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $artifact.sha256) { throw "$($item.packageId): validated artifact hash mismatch." } } }
}
$publicationOrder = if ($candidate) { @($candidate.publicationOrder) } else { @($manifest.packages.packageId) }
$manifestById = @{}; $manifest.packages | ForEach-Object { $manifestById[$_.packageId] = $_ }
$states = @()
foreach ($package in $manifest.packages) {
    $id = [string]$package.packageId
    $url = "https://api.nuget.org/v3-flatcontainer/$($id.ToLowerInvariant())/index.json"
    try {
        $versions = @((Invoke-RestMethod -Uri $url -ErrorAction Stop).versions)
        $exists = $versions -contains [string]$manifest.version
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 404) { $exists = $false } else { throw }
    }
    $states += [pscustomobject]@{ packageId = $id; version = [string]$manifest.version; exists = $exists }
}
$present = @($states | Where-Object exists)
if ($present.Count -ne 0 -and $present.Count -ne $states.Count) {
    $diagnostic = $states | ForEach-Object { "$($_.packageId)=$($_.exists)" }
    throw "Incoherent remote product-train state; no package was pushed: $($diagnostic -join ', ')."
}
$state = if ($present.Count -eq $states.Count) { "fully-published" } else { "fully-available" }
$plan = [ordered]@{ generatedAtUtc = [DateTime]::UtcNow.ToString('O'); productTrain = [string]$manifest.productTrain; version = [string]$manifest.version; state = $state; publicationEnabled = [bool]$Publish; order = $publicationOrder; packages = $states; published = @(); notPublished = @($states.packageId); nextManualAction = "Review validated hashes and explicitly enable publication in a follow-up change." }
$reportRoot = Join-Path $artifactRoot "reports"; New-Item $reportRoot -ItemType Directory -Force | Out-Null; $plan | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $reportRoot "publication-plan.json")
if ($present.Count -eq $states.Count) { Write-Host "All packages already exist; nothing to publish."; return }
if (-not $Publish) { Write-Host "Dry run passed: all manifested package IDs are available at $($manifest.version)."; return }
if ([string]::IsNullOrWhiteSpace($ApiKey)) { throw "An API key is required for publication." }
foreach ($packageId in $publicationOrder) {
    $package = $manifestById[$packageId]
    $archive = Join-Path $packagesPath "$($package.packageId).$($manifest.version).nupkg"
    if (-not (Test-Path $archive)) { throw "Validated candidate '$archive' is missing." }
}
foreach ($packageId in $publicationOrder) {
    $package = $manifestById[$packageId]
    foreach ($extension in @("nupkg", "snupkg")) {
        $archive = Join-Path $packagesPath "$($package.packageId).$($manifest.version).$extension"
        if (Test-Path $archive) {
            & dotnet nuget push $archive --api-key $ApiKey --source "https://api.nuget.org/v3/index.json"
            if ($LASTEXITCODE -ne 0) { throw "Publication stopped after failure pushing '$archive'." }
        }
    }
}
