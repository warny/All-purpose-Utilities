<#
.SYNOPSIS
Builds the train in two isolated worktrees and compares every package entry.
#>
[CmdletBinding()]
param([string] $Configuration = "Release", [string] $ArtifactsPath = "artifacts")
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem
. (Join-Path $PSScriptRoot "Release.Common.ps1")
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-ProductTrainManifest $repoRoot
$artifactRoot = Resolve-RepositoryPath $repoRoot $ArtifactsPath
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) "omy-reproducibility-$([guid]::NewGuid().ToString('N'))"
$originalNuGetPackages = $env:NUGET_PACKAGES
$originalDotnetHome = $env:DOTNET_CLI_HOME

function Get-ArchiveEntries {
    param([string] $Path)
    $zip = [IO.Compression.ZipFile]::OpenRead($Path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $values = @{}
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName -eq '.signature.p7s' -or $entry.FullName -eq '_rels/.rels' -or $entry.FullName -like 'package/services/metadata/core-properties/*') { continue }
            $stream = $entry.Open()
            try { $values[$entry.FullName] = [Convert]::ToHexString($sha.ComputeHash($stream)).ToLowerInvariant() } finally { $stream.Dispose() }
        }
        return $values
    } finally { $sha.Dispose(); $zip.Dispose() }
}

try {
    New-Item $temporaryRoot -ItemType Directory -Force | Out-Null
    $isCi = $env:CI -eq 'true'
    $inputSnapshot = if ($isCi) { 'committed-head' } else { 'working-tree-overlay' }
    $workingFiles = if ($isCi) { @() } else {
        @(
            & git -C $repoRoot diff --name-only HEAD
            if ($LASTEXITCODE -ne 0) { throw "Unable to enumerate modified repository inputs." }
            & git -C $repoRoot ls-files --others --exclude-standard
            if ($LASTEXITCODE -ne 0) { throw "Unable to enumerate untracked repository inputs." }
        ) | Sort-Object -Unique
    }
    foreach ($run in 1..2) {
        $worktree = Join-Path $temporaryRoot "source-$run"
        & git -C $repoRoot worktree add --detach $worktree HEAD | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Unable to create isolated worktree $run." }
        # CI compares the committed SHA directly. Local runs overlay only actual working-tree
        # differences so their result is explicitly distinct from a committed-HEAD result.
        foreach ($relative in $workingFiles) {
            if ($relative -match '(^|/)(artifacts|bin|obj)(/|$)') { continue }
            $source = Join-Path $repoRoot $relative
            $destination = Join-Path $worktree $relative
            if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
                Remove-Item -LiteralPath $destination -Force -ErrorAction SilentlyContinue
                continue
            }
            New-Item (Split-Path -Parent $destination) -ItemType Directory -Force | Out-Null
            Copy-Item -LiteralPath $source -Destination $destination -Force
        }
        $env:NUGET_PACKAGES = Join-Path $temporaryRoot "nuget-$run"
        $env:DOTNET_CLI_HOME = Join-Path $temporaryRoot "dotnet-home-$run"
        & dotnet restore (Join-Path $worktree 'Utils.sln') --packages $env:NUGET_PACKAGES
        if ($LASTEXITCODE -ne 0) { throw "Reproducibility restore $run failed in '$worktree'." }
        & (Join-Path $worktree 'eng/pack-product-train.ps1') -Configuration $Configuration -ArtifactsPath 'artifacts-reproducibility'
        if ($LASTEXITCODE -ne 0) { throw "Reproducibility build $run failed in '$worktree'." }
        $destination = Join-Path $artifactRoot "reproducibility/run$run/packages"
        New-Item $destination -ItemType Directory -Force | Out-Null
        Copy-Item (Join-Path $worktree 'artifacts-reproducibility/packages/*') $destination -Force
    }
    $comparisons = @()
    foreach ($package in $manifest.packages) {
        foreach ($extension in @('nupkg', 'snupkg')) {
            $name = "$($package.packageId).$($manifest.version).$extension"
            $one = Join-Path $artifactRoot "reproducibility/run1/packages/$name"
            $two = Join-Path $artifactRoot "reproducibility/run2/packages/$name"
            if (-not (Test-Path $one) -or -not (Test-Path $two)) { throw "$name is missing from one isolated build." }
            $hashOne = (Get-FileHash $one -Algorithm SHA256).Hash.ToLowerInvariant(); $hashTwo = (Get-FileHash $two -Algorithm SHA256).Hash.ToLowerInvariant()
            $entriesOne = Get-ArchiveEntries $one; $entriesTwo = Get-ArchiveEntries $two; $differences = @()
            foreach ($path in @($entriesOne.Keys + $entriesTwo.Keys | Sort-Object -Unique)) {
                if ($entriesOne[$path] -ne $entriesTwo[$path]) { $differences += [ordered]@{ file=$path; firstHash=$entriesOne[$path]; secondHash=$entriesTwo[$path]; difference='content' } }
            }
            $result = if ($hashOne -eq $hashTwo) { 'bit-identical' } elseif (-not $differences) { 'logically-identical-after-zip-normalization' } else { 'different' }
            if ($result -eq 'different') { throw "$name contains path-isolated reproducibility differences: $($differences.file -join ', ')." }
            $comparisons += [ordered]@{ packageId=$package.packageId; version=[string]$manifest.version; artifact=$name; result=$result; firstSha256=$hashOne; secondSha256=$hashTwo; differences=$differences }
        }
    }
    Write-ReleaseJson ([ordered]@{ productTrain=[string]$manifest.productTrain; version=[string]$manifest.version; isolation='two-distinct-git-worktrees'; inputSnapshot=$inputSnapshot; artifacts=$comparisons }) (Join-Path $artifactRoot 'reports/reproducibility-report.json')
} finally {
    $env:NUGET_PACKAGES = $originalNuGetPackages
    $env:DOTNET_CLI_HOME = $originalDotnetHome
    foreach ($run in 1..2) {
        $worktree = Join-Path $temporaryRoot "source-$run"
        if (Test-Path $worktree) { & git -C $repoRoot worktree remove --force $worktree | Out-Null }
    }
    if (Test-Path $temporaryRoot) { Remove-Item $temporaryRoot -Recurse -Force -ErrorAction SilentlyContinue }
}
