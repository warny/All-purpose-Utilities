[CmdletBinding()]
param(
    [string] $ArtifactsPath = "artifacts",
    [switch] $SkipSourceLink
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content (Join-Path $PSScriptRoot "parser-release-manifest.json") -Raw | ConvertFrom-Json
$versionsXml = [xml](Get-Content (Join-Path $repoRoot "Directory.Build.props") -Raw)
$packageDirectory = Join-Path ([IO.Path]::GetFullPath((Join-Path $repoRoot $ArtifactsPath))) "packages"
$expected = @{}
foreach ($item in $manifest.packages) {
    $version = ([string]$versionsXml.Project.PropertyGroup.($item.versionProperty)).Trim()
    if ([string]::IsNullOrWhiteSpace($version)) { throw "Missing version property '$($item.versionProperty)'." }
    $expected[$item.packageId.ToLowerInvariant()] = $version
}

$packages = @(Get-ChildItem $packageDirectory -Filter *.nupkg -File | Where-Object Extension -eq ".nupkg")
if ($packages.Count -ne $expected.Count) { throw "Expected $($expected.Count) nupkg files, found $($packages.Count)." }
$seen = @{}
$internalDependencies = @{
    "omy.utils.parser" = @("omy.utils.parser.source", "omy.utils.parser.diagnostics", "omy.utils.parser.antlr4.common")
    "omy.utils.parser.diagnostics" = @("omy.utils.parser.source")
    "omy.utils.parser.expressions" = @("omy.utils.parser", "omy.utils")
}

foreach ($file in $packages) {
    $archive = [IO.Compression.ZipFile]::OpenRead($file.FullName)
    try {
        $entries = @($archive.Entries | ForEach-Object FullName)
        $nuspecEntry = @($archive.Entries | Where-Object FullName -like "*.nuspec")
        if ($nuspecEntry.Count -ne 1) { throw "$($file.Name): expected one nuspec." }
        $reader = [IO.StreamReader]::new($nuspecEntry[0].Open())
        try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $metadata = $nuspec.package.metadata
        $id = ([string]$metadata.id).ToLowerInvariant()
        $version = [string]$metadata.version
        if (-not $expected.ContainsKey($id)) { throw "$($file.Name): unexpected package id '$id'." }
        if ($version -ne $expected[$id]) { throw "${id}: expected version '$($expected[$id])', got '$version'." }
        if ($seen.ContainsKey($id)) { throw "Duplicate package '$id'." }
        $seen[$id] = $true
        if (-not ($entries -contains "README.md")) { throw "${id}: README.md is missing." }
        if (-not ($entries -contains "LICENSE-apache-2.0.txt")) { throw "${id}: license file is missing." }
        if (-not ($entries | Where-Object { $_ -like "lib/*/*.dll" -or $_ -like "analyzers/dotnet/cs/*.dll" })) { throw "${id}: no assembly asset found." }
        if (-not ($entries | Where-Object { $_ -like "lib/*/*.xml" })) {
            if ($id -ne "omy.utils.parser.generators") { throw "${id}: XML documentation is missing." }
        }
        $invalid = @($entries | Where-Object { $_ -match '(^|/)(obj|bin/Debug|tmp|temp)(/|$)' -or $_ -match '^[A-Za-z]:\\' })
        if ($invalid.Count) { throw "${id}: forbidden archive entries: $($invalid -join ', ')." }
        $dllNames = @($entries | Where-Object { $_ -like "*.dll" } | ForEach-Object { [IO.Path]::GetFileName($_).ToLowerInvariant() })
        if (($dllNames | Group-Object | Where-Object Count -gt 1).Count) { throw "${id}: duplicate DLL names found." }
        $deps = @{}
        @($metadata.dependencies.group.dependency) + @($metadata.dependencies.dependency) | Where-Object { $null -ne $_ } | ForEach-Object {
            $deps[([string]$_.id).ToLowerInvariant()] = ([string]$_.version).Trim('[',']')
        }
        foreach ($dependency in @($internalDependencies[$id] | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
            if (-not $deps.ContainsKey($dependency)) { throw "${id}: missing dependency '$dependency'." }
            if ($deps[$dependency] -ne $expected[$dependency]) { throw "${id}: dependency '$dependency' has '$($deps[$dependency])', expected '$($expected[$dependency])'." }
        }
        if ($id -eq "omy.utils.parser.generators") {
            foreach ($assembly in @("Utils.Parser.Generators.dll", "Utils.Parser.Diagnostics.dll", "Utils.Parser.Source.dll", "Utils.Parser.Antlr4.Common.dll")) {
                if (-not ($entries -contains "analyzers/dotnet/cs/netstandard2.0/$assembly")) { throw "${id}: analyzer dependency '$assembly' is missing." }
            }
            if (-not ($entries -contains "buildTransitive/omy.Utils.Parser.Generators.targets")) { throw "${id}: buildTransitive targets are missing." }
            if ($deps.Keys | Where-Object { $_ -like "omy.utils.parser*" }) { throw "${id}: analyzer support assemblies must not be runtime dependencies." }
        }
        $symbolPath = Join-Path $packageDirectory "$($metadata.id).$version.snupkg"
        if (-not (Test-Path $symbolPath)) { throw "${id}: symbol package is missing." }
        if (-not $SkipSourceLink) {
            if ([string]$metadata.repository.url -ne "https://github.com/warny/All-purpose-Utilities") { throw "${id}: repository URL is incorrect." }
            $commit = (& git -C $repoRoot rev-parse HEAD).Trim()
            if ([string]$metadata.repository.commit -ne $commit) { throw "${id}: repository commit does not match $commit." }
            $symbols = [IO.Compression.ZipFile]::OpenRead($symbolPath)
            try {
                if (-not ($symbols.Entries | Where-Object FullName -like "*.pdb")) { throw "${id}: portable PDB is missing from symbols." }
            } finally { $symbols.Dispose() }
        }
        Write-Host "Inspect: $($metadata.id) $version passed"
    } finally { $archive.Dispose() }
}
foreach ($id in $expected.Keys) { if (-not $seen.ContainsKey($id)) { throw "Package '$id' is missing." } }
