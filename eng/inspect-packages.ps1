<#
.SYNOPSIS
Inspects every package in the global product train using manifest and graph expectations.
#>
[CmdletBinding()]
param([string] $ArtifactsPath = "artifacts", [Alias("SkipSourceLink")][switch] $SkipRepositoryMetadataAndSymbols)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Reflection.Metadata
. (Join-Path $PSScriptRoot "Release.Common.ps1")
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-ProductTrainManifest $repoRoot
$artifactRoot = Resolve-RepositoryPath $repoRoot $ArtifactsPath
$packageDirectory = Join-Path $artifactRoot "packages"
$graph = Get-Content (Join-Path $artifactRoot "reports/package-graph.json") -Raw | ConvertFrom-Json
$manifestById = @{}; $manifest.packages | ForEach-Object { $manifestById[$_.packageId.ToLowerInvariant()] = $_ }
$expectedRuntimeDependencies = @{}
foreach ($package in $manifest.packages) { $expectedRuntimeDependencies[$package.packageId.ToLowerInvariant()] = @($graph.edges | Where-Object { $_.from -eq $package.packageId -and $_.relationship -eq 'NuGet runtime dependency' } | ForEach-Object { $_.to.ToLowerInvariant() }) }
$archives = @(Get-ChildItem $packageDirectory -Filter *.nupkg -File | Where-Object Extension -eq '.nupkg')
if ($archives.Count -ne $manifest.packages.Count) { throw "Expected $($manifest.packages.Count) nupkg files, found $($archives.Count)." }
$seen = @{}; $inspection = @(); $commit = (& git -C $repoRoot rev-parse HEAD).Trim()
foreach ($file in $archives) {
    $archive = [IO.Compression.ZipFile]::OpenRead($file.FullName)
    try {
        $entries = @($archive.Entries | ForEach-Object FullName)
        $nuspecEntry = @($archive.Entries | Where-Object FullName -like '*.nuspec')
        if ($nuspecEntry.Count -ne 1) { throw "$($file.Name): expected exactly one nuspec." }
        $reader = [IO.StreamReader]::new($nuspecEntry[0].Open()); try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $metadata = $nuspec.package.metadata; $id = ([string]$metadata.id).ToLowerInvariant(); $version = [string]$metadata.version
        if (-not $manifestById.ContainsKey($id)) { throw "$($file.Name): unexpected package '$id'." }
        $definition = $manifestById[$id]
        if ($version -ne $manifest.version) { throw "${id}: version '$version' differs from '$($manifest.version)'." }
        if ($seen.ContainsKey($id)) { throw "Duplicate package '$id'." }; $seen[$id] = $true
        foreach ($required in @('README.md', 'LICENSE-apache-2.0.txt')) { if ($entries -notcontains $required) { throw "${id}: '$required' is missing." } }
        $invalid = @($entries | Where-Object { $_ -match '(^|/)(obj|bin/Debug|tmp|temp)(/|$)' -or $_ -match '^[A-Za-z]:\\' })
        if ($invalid) { throw "${id}: forbidden archive entries: $($invalid -join ', ')." }
        $assemblyEntries = @($entries | Where-Object { $_ -like 'lib/*/*.dll' -or $_ -like 'ref/*/*.dll' -or $_ -like 'analyzers/dotnet/cs/*.dll' -or $_ -like 'analyzers/dotnet/cs/*/*.dll' })
        if (-not $assemblyEntries) { throw "${id}: no library or analyzer assembly found." }
        if ($definition.kind -eq 'library' -and -not ($entries | Where-Object { $_ -like 'lib/*/*.xml' })) { throw "${id}: XML documentation is missing." }
        if ($definition.kind -eq 'analyzer' -and -not ($assemblyEntries | Where-Object { $_ -like 'analyzers/dotnet/cs/*' })) { throw "${id}: analyzer is not under analyzers/dotnet/cs." }
        $deps = @{}; @($metadata.dependencies.group.dependency) + @($metadata.dependencies.dependency) | Where-Object { $null -ne $_ } | ForEach-Object { $deps[([string]$_.id).ToLowerInvariant()] = [string]$_.version }
        $actualInternal = @($deps.Keys | Where-Object { $manifestById.ContainsKey($_) } | Sort-Object)
        $expectedInternal = @($expectedRuntimeDependencies[$id] | Sort-Object)
        if (($actualInternal -join ',') -ne ($expectedInternal -join ',')) { throw "${id}: internal dependencies '$($actualInternal -join ',')' differ from graph '$($expectedInternal -join ',')'." }
        foreach ($dependency in $actualInternal) {
            if ($deps[$dependency] -notin @("[$($manifest.version)]")) { throw "${id}: dependency '$dependency' must use the exact candidate version, not '$($deps[$dependency])'." }
        }
        if ([string]$metadata.repository.url -ne $manifest.repository) { throw "${id}: repository URL is incorrect." }
        if (-not $SkipRepositoryMetadataAndSymbols -and [string]$metadata.repository.commit -ne $commit) { throw "${id}: repository commit is incorrect." }
        $symbolPath = Join-Path $packageDirectory "$($definition.packageId).$version.snupkg"
        if ($definition.symbolPackage -and -not (Test-Path $symbolPath)) { throw "${id}: symbol package is missing." }
        $assemblyReports = @()
        foreach ($dllPath in $assemblyEntries) {
            $stream = $archive.GetEntry($dllPath).Open(); $copy = [IO.MemoryStream]::new(); $stream.CopyTo($copy); $copy.Position = 0
            $readerPe = [Reflection.PortableExecutable.PEReader]::new($copy)
            try {
                $assemblyVersion = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($readerPe).GetAssemblyDefinition().Version
                if ($assemblyVersion -ne [Version]'2.0.0.0') { throw "${id}: '$dllPath' assembly version is '$assemblyVersion'." }
                $temporaryDll = [IO.Path]::GetTempFileName(); [IO.File]::WriteAllBytes($temporaryDll, $copy.ToArray())
                try { $fileInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($temporaryDll); if ($fileInfo.FileVersion -ne '2.0.0.0') { throw "${id}: '$dllPath' file version is '$($fileInfo.FileVersion)'." }; if (-not $fileInfo.ProductVersion.StartsWith("$($manifest.version)+")) { throw "${id}: '$dllPath' informational version '$($fileInfo.ProductVersion)' lacks candidate version and commit." } } finally { Remove-Item $temporaryDll -Force }
            } finally { $readerPe.Dispose(); $copy.Dispose(); $stream.Dispose() }
            $assemblyReports += [ordered]@{ path = $dllPath; assemblyVersion = $assemblyVersion.ToString(); fileVersion = $fileInfo.FileVersion; informationalVersion = $fileInfo.ProductVersion }
        }
        foreach ($embedded in @($definition.embeddedAssemblies | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
            if (-not ($assemblyEntries | Where-Object { [IO.Path]::GetFileName($_) -eq $embedded })) { throw "${id}: declared embedded assembly '$embedded' is missing." }
        }
        foreach ($asset in @($definition.buildTransitiveAssets | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) { if ($entries -notcontains $asset) { throw "${id}: declared asset '$asset' is missing." } }
        $inspection += [ordered]@{ packageId = $definition.packageId; version = $version; kind = $definition.kind; frameworks = @($definition.targetFrameworks); dependencies = $deps; assemblies = $assemblyReports; passed = $true }
        Write-Host "Inspect: $($definition.packageId) $version passed"
    } finally { $archive.Dispose() }
}
foreach ($id in $manifestById.Keys) { if (-not $seen.ContainsKey($id)) { throw "Package '$id' is missing." } }
# Embedded parser support assemblies must remain byte-identical to their runtime package copies.
$generator = $manifest.packages | Where-Object packageId -eq 'omy.Utils.Parser.Generators'
if ($generator) {
    $generatorArchive = [IO.Compression.ZipFile]::OpenRead((Join-Path $packageDirectory "$($generator.packageId).$($manifest.version).nupkg"))
    try { foreach ($name in @($generator.embeddedAssemblies)) {
        $embeddedEntry = $generatorArchive.Entries | Where-Object { [IO.Path]::GetFileName($_.FullName) -eq $name } | Select-Object -First 1
        $runtimeId = "omy.$([IO.Path]::GetFileNameWithoutExtension($name))"; $runtimePackage = $manifest.packages | Where-Object packageId -eq $runtimeId
        $runtimeArchive = [IO.Compression.ZipFile]::OpenRead((Join-Path $packageDirectory "$($runtimePackage.packageId).$($manifest.version).nupkg"))
        try { $runtimeEntry = $runtimeArchive.Entries | Where-Object { [IO.Path]::GetFileName($_.FullName) -eq $name } | Select-Object -First 1; $a=$embeddedEntry.Open(); $b=$runtimeEntry.Open(); $sha=[Security.Cryptography.SHA256]::Create(); try { $ha=[Convert]::ToHexString($sha.ComputeHash($a)); $hb=[Convert]::ToHexString($sha.ComputeHash($b)); if($ha -ne $hb){throw "Embedded '$name' differs from runtime package."} } finally {$sha.Dispose();$a.Dispose();$b.Dispose()} } finally {$runtimeArchive.Dispose()}
    } } finally {$generatorArchive.Dispose()}
}
Write-ReleaseJson ([ordered]@{ version = [string]$manifest.version; packages = $inspection }) (Join-Path $artifactRoot 'reports/package-inspection.json')
