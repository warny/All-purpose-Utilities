[CmdletBinding()]
param(
    [string] $ArtifactsPath = "artifacts",
    [Alias("SkipSourceLink")][switch] $SkipRepositoryMetadataAndSymbols
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Reflection.Metadata
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
$expectedDependencies = @{
    "omy.utils" = @("system.text.encoding.codepages")
    "omy.utils.parser.source" = @()
    "omy.utils.parser.antlr4.common" = @()
    "omy.utils.parser" = @("omy.utils.parser.source", "omy.utils.parser.diagnostics", "omy.utils.parser.antlr4.common")
    "omy.utils.parser.diagnostics" = @("omy.utils.parser.source")
    "omy.utils.parser.expressions" = @("omy.utils.parser", "omy.utils")
    "omy.utils.parser.generators" = @()
}
$expectedFrameworks = @{
    "omy.utils" = "net8.0"
    "omy.utils.parser" = "net8.0"
    "omy.utils.parser.expressions" = "net8.0"
    "omy.utils.parser.source" = "netstandard2.0"
    "omy.utils.parser.diagnostics" = "netstandard2.0"
    "omy.utils.parser.antlr4.common" = "netstandard2.0"
    "omy.utils.parser.generators" = "netstandard2.0"
}
$expectedDlls = @{
    "omy.utils" = @("lib/net8.0/Utils.dll")
    "omy.utils.parser" = @("lib/net8.0/Utils.Parser.dll")
    "omy.utils.parser.expressions" = @("lib/net8.0/Utils.Parser.Expressions.dll")
    "omy.utils.parser.source" = @("lib/netstandard2.0/Utils.Parser.Source.dll")
    "omy.utils.parser.diagnostics" = @("lib/netstandard2.0/Utils.Parser.Diagnostics.dll")
    "omy.utils.parser.antlr4.common" = @("lib/netstandard2.0/Utils.Parser.Antlr4.Common.dll")
    "omy.utils.parser.generators" = @(
        "analyzers/dotnet/cs/netstandard2.0/Utils.Parser.Generators.dll",
        "analyzers/dotnet/cs/netstandard2.0/Utils.Parser.Diagnostics.dll",
        "analyzers/dotnet/cs/netstandard2.0/Utils.Parser.Source.dll",
        "analyzers/dotnet/cs/netstandard2.0/Utils.Parser.Antlr4.Common.dll")
}

<#
.SYNOPSIS
Computes a SHA-256 hash for one file stored in a NuGet ZIP archive.
#>
function Get-PackageEntryHash {
    param(
        [Parameter(Mandatory)][string] $PackagePath,
        [Parameter(Mandatory)][string] $EntryPath
    )

    $zip = [IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $entry = $zip.GetEntry($EntryPath) ?? (throw "Archive '$PackagePath' does not contain '$EntryPath'.")
        $stream = $entry.Open()
        $algorithm = [Security.Cryptography.SHA256]::Create()
        try { return [Convert]::ToHexString($algorithm.ComputeHash($stream)) }
        finally { $algorithm.Dispose(); $stream.Dispose() }
    } finally { $zip.Dispose() }
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
        $actualDependencyIds = @($deps.Keys | Sort-Object)
        $expectedDependencyIds = @($expectedDependencies[$id] | Sort-Object)
        if (($actualDependencyIds -join ",") -ne ($expectedDependencyIds -join ",")) {
            throw "${id}: dependencies '$($actualDependencyIds -join ', ')' do not match expected '$($expectedDependencyIds -join ', ')'."
        }
        foreach ($dependency in @($expectedDependencies[$id] | Where-Object { $_ -like "omy.*" })) {
            if (-not $deps.ContainsKey($dependency)) { throw "${id}: missing dependency '$dependency'." }
            if ($deps[$dependency] -ne $expected[$dependency]) { throw "${id}: dependency '$dependency' has '$($deps[$dependency])', expected '$($expected[$dependency])'." }
        }
        $actualDlls = @($entries | Where-Object { $_ -like "*.dll" } | Sort-Object)
        $requiredDlls = @($expectedDlls[$id] | Sort-Object)
        if (($actualDlls -join ",") -ne ($requiredDlls -join ",")) {
            throw "${id}: DLL assets '$($actualDlls -join ', ')' do not match expected '$($requiredDlls -join ', ')'."
        }
        $candidateVersion = [Version]($version -split "-")[0]
        $expectedAssemblyVersion = [Version]::new(
            $candidateVersion.Major,
            $candidateVersion.Minor,
            [Math]::Max(0, $candidateVersion.Build),
            [Math]::Max(0, $candidateVersion.Revision))
        foreach ($dllPath in $actualDlls) {
            $dllEntry = $archive.GetEntry($dllPath)
            $dllStream = $dllEntry.Open()
            $seekableDll = [IO.MemoryStream]::new()
            $dllStream.CopyTo($seekableDll)
            $seekableDll.Position = 0
            $peReader = [Reflection.PortableExecutable.PEReader]::new($seekableDll)
            try {
                $metadataReader = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($peReader)
                $assemblyVersion = $metadataReader.GetAssemblyDefinition().Version
                if ($assemblyVersion -ne $expectedAssemblyVersion) {
                    throw "${id}: '$dllPath' has assembly version '$assemblyVersion', expected '$expectedAssemblyVersion'."
                }
            } finally { $peReader.Dispose(); $seekableDll.Dispose(); $dllStream.Dispose() }
        }
        if ($id -ne "omy.utils.parser.generators") {
            $frameworkPath = "lib/$($expectedFrameworks[$id])/"
            if (-not ($entries | Where-Object { $_ -like "$frameworkPath*" })) { throw "${id}: expected framework '$($expectedFrameworks[$id])' is missing." }
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
        if (-not $SkipRepositoryMetadataAndSymbols) {
            if ([string]$metadata.repository.url -ne "https://github.com/warny/All-purpose-Utilities") { throw "${id}: repository URL is incorrect." }
            $commit = (& git -C $repoRoot rev-parse HEAD).Trim()
            if ([string]$metadata.repository.commit -ne $commit) { throw "${id}: repository commit does not match $commit." }
            $symbols = [IO.Compression.ZipFile]::OpenRead($symbolPath)
            try {
                $pdbEntries = @($symbols.Entries | Where-Object FullName -like "*.pdb")
                if (-not $pdbEntries) { throw "${id}: portable PDB is missing from symbols." }
                foreach ($pdbEntry in $pdbEntries) {
                    $pdbStream = $pdbEntry.Open()
                    try {
                        $signature = [byte[]]::new(4)
                        if ($pdbStream.Read($signature, 0, 4) -ne 4 -or [Text.Encoding]::ASCII.GetString($signature) -ne "BSJB") {
                            throw "${id}: '$($pdbEntry.FullName)' is not a portable PDB."
                        }
                    } finally { $pdbStream.Dispose() }
                }
            } finally { $symbols.Dispose() }
        }
        Write-Host "Inspect: $($metadata.id) $version passed"
    } finally { $archive.Dispose() }
}
foreach ($id in $expected.Keys) { if (-not $seen.ContainsKey($id)) { throw "Package '$id' is missing." } }
$generatorPackage = Join-Path $packageDirectory "omy.Utils.Parser.Generators.$($expected['omy.utils.parser.generators']).nupkg"
$supportPackages = @{
    "Utils.Parser.Source.dll" = @("omy.Utils.Parser.Source", "lib/netstandard2.0/Utils.Parser.Source.dll")
    "Utils.Parser.Diagnostics.dll" = @("omy.Utils.Parser.Diagnostics", "lib/netstandard2.0/Utils.Parser.Diagnostics.dll")
    "Utils.Parser.Antlr4.Common.dll" = @("omy.Utils.Parser.Antlr4.Common", "lib/netstandard2.0/Utils.Parser.Antlr4.Common.dll")
}
foreach ($supportAssembly in $supportPackages.Keys) {
    $packageId = $supportPackages[$supportAssembly][0]
    $packagePath = Join-Path $packageDirectory "$packageId.$($expected[$packageId.ToLowerInvariant()]).nupkg"
    $analyzerHash = Get-PackageEntryHash $generatorPackage "analyzers/dotnet/cs/netstandard2.0/$supportAssembly"
    $runtimeHash = Get-PackageEntryHash $packagePath $supportPackages[$supportAssembly][1]
    if ($analyzerHash -ne $runtimeHash) { throw "Generator support assembly '$supportAssembly' is not byte-identical to its candidate package assembly." }
}
if (-not $SkipRepositoryMetadataAndSymbols) {
    Write-Warning "Repository metadata, commit identity, symbol-package presence, and portable PDB format were validated; SourceLink document mappings and source retrieval were not validated."
}
