<#
.SYNOPSIS
Builds (or reuses) the Utils.Parser.VisualStudio VSIX in Release and validates the produced artifact:
exactly one .vsix, a well-formed manifest with a stable Id/Publisher and a version conforming to the
documented policy (docs/releasing/VisualStudioExtension.md), and the out-of-process worker payload
required at runtime by PluginWorkerProcess.TryCreate(). Never publishes anything.
#>
[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()][string] $Configuration = "Release",
    [string] $ProjectPath = "Utils.Parser.VisualStudio/Utils.Parser.VisualStudio.csproj",
    [string] $ArtifactsPath = "artifacts",
    [switch] $SkipBuild,
    [TimeSpan] $BuildTimeout = ([TimeSpan]::FromMinutes(15))
)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Release.Common.ps1")
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectFullPath = Resolve-RepositoryPath $repoRoot $ProjectPath
$artifactRoot = Resolve-RepositoryPath $repoRoot $ArtifactsPath
$workRoot = Join-Path $artifactRoot 'vsix-validation'
Remove-Item $workRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item $workRoot -ItemType Directory -Force | Out-Null

<#
    Recorded identity: the VSIX Id must never change after the first Marketplace publication (Visual
    Studio and the Marketplace use it to recognize updates to the same extension), and the Publisher
    must match whatever Marketplace Publisher account will host it. Update these two values only as
    part of a deliberate, documented identity change - never as a side effect of an unrelated edit.
#>
$expectedVsixId = 'Utils.Parser.VisualStudio.ef18346f-f79e-4e44-86f4-bf8094951570'
$expectedPublisher = 'Olivier MARTY'

if (-not $SkipBuild) {
    Invoke-ReleaseGate -Name 'vsix-build' -DisplayName 'Build Utils.Parser.VisualStudio (Release)' -Action {
        Invoke-NativeCommand -FilePath 'dotnet' -ArgumentList @('build', $projectFullPath, '--configuration', $Configuration) -Timeout $BuildTimeout -LogPath (Join-Path $workRoot 'build.log') | Out-Null
    }
}

$projectDir = Split-Path -Parent $projectFullPath
$vsixCandidates = @(Get-ChildItem -Path (Join-Path (Join-Path $projectDir 'bin') $Configuration) -Filter '*.vsix' -File -Recurse -ErrorAction SilentlyContinue)
if ($vsixCandidates.Count -eq 0) { throw "No .vsix file was produced under 'bin/$Configuration'. Build the project first or check CreateVsixContainer." }
if ($vsixCandidates.Count -gt 1) { throw "Expected exactly one .vsix file, found $($vsixCandidates.Count): $($vsixCandidates.FullName -join ', ')." }
$vsixPath = $vsixCandidates[0].FullName

$extractDir = Join-Path $workRoot 'extracted'
Expand-ZipArchive $vsixPath $extractDir

$manifestPath = Join-Path $extractDir 'extension.vsixmanifest'
if (-not (Test-Path $manifestPath)) { throw "extension.vsixmanifest is missing from the VSIX archive." }
[xml]$manifestXml = Get-Content $manifestPath -Raw
$identity = $manifestXml.PackageManifest.Metadata.Identity
if ($null -eq $identity) { throw "extension.vsixmanifest has no <Identity> element." }
if ([string]::IsNullOrWhiteSpace($identity.Id)) { throw "extension.vsixmanifest <Identity> has an empty Id." }
if ($identity.Id -ne $expectedVsixId) { throw "VSIX Id changed: expected '$expectedVsixId', found '$($identity.Id)'. The Id must stay stable across publications; update `$expectedVsixId` in this script only for a deliberate, documented identity change." }
if ($identity.Publisher -ne $expectedPublisher) { throw "VSIX Publisher changed: expected '$expectedPublisher', found '$($identity.Publisher)'." }
if ([string]::IsNullOrWhiteSpace($manifestXml.PackageManifest.Metadata.DisplayName)) { throw "extension.vsixmanifest has an empty DisplayName." }

$vsixVersion = $identity.Version
if ($vsixVersion -notmatch '^\d+\.\d+\.\d+$') { throw "VSIX version '$vsixVersion' is not a plain Major.Minor.Build value." }

[xml]$buildProps = Get-Content (Join-Path $repoRoot 'Directory.Build.props') -Raw
$productTrainVersion = ([string]$buildProps.Project.PropertyGroup.ProductTrainVersion).Trim()
$isProductTrainPrerelease = $productTrainVersion -match '-'
if ($isProductTrainPrerelease) {
    if ($vsixVersion -notmatch '^0\.0\.\d+$') { throw "Product train '$productTrainVersion' is a 2.0.0 prerelease, so the VSIX version must be '0.0.x' per docs/releasing/VisualStudioExtension.md; found '$vsixVersion'." }
} else {
    if ($vsixVersion -ne $productTrainVersion) { throw "Product train '$productTrainVersion' is stable, so the VSIX version must match it exactly per docs/releasing/VisualStudioExtension.md; found '$vsixVersion'." }
}

$workerExeRelativePath = 'worker/Utils.Parser.VisualStudio.Worker.exe'
$workerExePath = Join-Path $extractDir 'worker/Utils.Parser.VisualStudio.Worker.exe'
if (-not (Test-Path $workerExePath)) { throw "'$workerExeRelativePath' is missing from the VSIX. PluginWorkerProcess.TryCreate() resolves the worker at '<extensionDir>\worker\Utils.Parser.VisualStudio.Worker.exe'; without it every installed extension silently degrades to in-process-only classification." }

$workerDepsPath = Join-Path $extractDir 'worker/Utils.Parser.VisualStudio.Worker.deps.json'
if (-not (Test-Path $workerDepsPath)) { throw "'worker/Utils.Parser.VisualStudio.Worker.deps.json' is missing from the VSIX." }
$workerDeps = Get-Content $workerDepsPath -Raw | ConvertFrom-Json
$workerTargetName = $workerDeps.runtimeTarget.name
$workerTarget = $workerDeps.targets.$workerTargetName
$expectedWorkerFiles = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($library in $workerTarget.PSObject.Properties) {
    $runtimeFiles = $library.Value.runtime
    if ($null -eq $runtimeFiles) { continue }
    foreach ($runtimeFile in $runtimeFiles.PSObject.Properties) {
        [void]$expectedWorkerFiles.Add((Split-Path -Leaf $runtimeFile.Name))
    }
}
[void]$expectedWorkerFiles.Add('Utils.Parser.VisualStudio.Worker.dll')
[void]$expectedWorkerFiles.Add('Utils.Parser.VisualStudio.Worker.exe')
[void]$expectedWorkerFiles.Add('Utils.Parser.VisualStudio.Worker.runtimeconfig.json')
$actualWorkerFiles = @(Get-ChildItem (Join-Path $extractDir 'worker') -File | Select-Object -ExpandProperty Name)
$missingWorkerFiles = @($expectedWorkerFiles | Where-Object { $_ -notin $actualWorkerFiles })
if ($missingWorkerFiles.Count -gt 0) { throw "The VSIX worker payload is incomplete; missing runtime file(s) required by the worker's own deps.json: $($missingWorkerFiles -join ', ')." }


<#
    UtilsParserVisualStudioExtension.cs declares a second, independent ExtensionMetadata(version: ...)
    consumed by Microsoft.VisualStudio.Extensibility at build time. It is not generated from the
    manifest, so nothing keeps it in sync automatically; catch drift here rather than at Marketplace
    review time.
#>
$extensionSourcePath = Join-Path $projectDir 'UtilsParserVisualStudioExtension.cs'
$extensionSource = Get-Content $extensionSourcePath -Raw
$extensionVersionMatch = [regex]::Match($extensionSource, 'new Version\((\d+),\s*(\d+),\s*(\d+)\)')
if (-not $extensionVersionMatch.Success) { throw "Could not find an ExtensionMetadata 'new Version(major, minor, build)' call in '$extensionSourcePath'." }
$extensionVersion = "{0}.{1}.{2}" -f $extensionVersionMatch.Groups[1].Value, $extensionVersionMatch.Groups[2].Value, $extensionVersionMatch.Groups[3].Value
if ($extensionVersion -ne $vsixVersion) { throw "Version mismatch: source.extension.vsixmanifest declares '$vsixVersion' but UtilsParserVisualStudioExtension.cs declares '$extensionVersion'. Update both together." }

$allEntries = @(Get-ChildItem $extractDir -File -Recurse | ForEach-Object { Get-RepositoryRelativePath $extractDir $_.FullName })
$suspiciousEntries = @($allEntries | Where-Object { $_ -match '(?i)\.(tmp|bak|log|received)$|(?i)(^|/)tests?[./]' })

Write-ReleaseJson ([ordered]@{
    commit          = (& git -C $repoRoot rev-parse HEAD).Trim()
    vsixPath        = (Get-RepositoryRelativePath $repoRoot $vsixPath)
    vsixId          = $identity.Id
    publisher       = $identity.Publisher
    displayName     = $manifestXml.PackageManifest.Metadata.DisplayName
    vsixVersion     = $vsixVersion
    productTrainVersion = $productTrainVersion
    versionPolicy   = if ($isProductTrainPrerelease) { 'provisional-0.0.x' } else { 'tracks-product-train' }
    entryCount      = $allEntries.Count
    workerFileCount = $actualWorkerFiles.Count
    suspiciousEntries = $suspiciousEntries
    passed          = $true
}) (Join-Path $artifactRoot 'reports/vsix-package.json')

if ($suspiciousEntries.Count -gt 0) {
    Write-Warning "The VSIX contains entries matching test/temp/backup patterns that may not belong in a shipped package: $($suspiciousEntries -join ', ')"
}

Write-Host "Validate: '$vsixPath' - Id, Publisher, and version policy hold; worker payload ($($actualWorkerFiles.Count) files) is complete."
