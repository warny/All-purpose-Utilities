<# Shared, side-effect-free helpers for the repository-wide release gates. #>

<# Loads the authoritative product-train manifest. #>
function Get-ProductTrainManifest {
    param([Parameter(Mandatory)][string] $RepositoryRoot)
    return Get-Content (Join-Path $RepositoryRoot "eng/product-train-manifest.json") -Raw | ConvertFrom-Json
}

<# Resolves a repository-relative path and rejects paths outside the repository. #>
function Resolve-RepositoryPath {
    param([Parameter(Mandatory)][string] $RepositoryRoot, [Parameter(Mandatory)][string] $Path)
    $resolved = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot $Path))
    if (-not $resolved.StartsWith([IO.Path]::GetFullPath($RepositoryRoot), [StringComparison]::OrdinalIgnoreCase)) { throw "Path '$Path' escapes the repository." }
    return $resolved
}

<# Evaluates release-relevant properties and item metadata through MSBuild. #>
function Get-EvaluatedProject {
    param([Parameter(Mandatory)][string] $ProjectPath, [string] $Configuration = "Release")
    $properties = "PackageId,Version,PackageVersion,VersionPrefix,VersionSuffix,AssemblyVersion,FileVersion,InformationalVersion,IsPackable,TargetFramework,TargetFrameworks,PackageType,GeneratePackageOnBuild,IsRoslynAnalyzer,BuildOutputTargetFolder,OutputType,SuppressDependenciesWhenPacking"
    $json = & dotnet msbuild $ProjectPath -nologo "-p:Configuration=$Configuration" "-getProperty:$properties" -getItem:ProjectReference,PackageReference
    if ($LASTEXITCODE -ne 0) { throw "MSBuild evaluation failed for '$ProjectPath'." }
    return (($json -join "`n") | ConvertFrom-Json)
}

<# Writes UTF-8 JSON after ensuring that its parent directory exists. #>
function Write-ReleaseJson {
    param([Parameter(Mandatory)] $Value, [Parameter(Mandatory)][string] $Path, [int] $Depth = 12)
    New-Item (Split-Path $Path -Parent) -ItemType Directory -Force | Out-Null
    $Value | ConvertTo-Json -Depth $Depth | Set-Content $Path -Encoding utf8
}

<# Extracts a ZIP-compatible archive without requiring a .zip file extension. #>
function Expand-ZipArchive {
    param(
        [Parameter(Mandatory)][string] $ArchivePath,
        [Parameter(Mandatory)][string] $DestinationPath
    )
    $archive = [IO.Path]::GetFullPath($ArchivePath)
    $destination = [IO.Path]::GetFullPath($DestinationPath)
    if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) { throw "Archive '$archive' does not exist." }
    Remove-Item -LiteralPath $destination -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -Path $destination -ItemType Directory -Force | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory($archive, $destination, $true)
}

<# Computes a deterministic fingerprint for emitted compiler-generated files. #>
function Get-GeneratedOutputFingerprint {
    param([Parameter(Mandatory)][string] $Path)

    $files = @(Get-ChildItem $Path -File -Recurse | Sort-Object FullName)
    if (-not $files) {
        throw "No compiler-generated files were emitted under '$Path'."
    }
    $entries = @($files | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($Path, $_.FullName).Replace([char]0x5c, [char]0x2f)
        "$relative=$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())"
    })
    $content = [Text.Encoding]::UTF8.GetBytes(($entries -join "`n"))
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($content)).ToLowerInvariant()
}

<# Normalizes either platform directory separator to a forward slash. #>
function ConvertTo-RepositoryPath {
    param([Parameter(Mandatory)][string] $Path)
    return $Path.Replace([char]0x5c, [char]0x2f).Replace([IO.Path]::DirectorySeparatorChar, [char]0x2f).Replace([IO.Path]::AltDirectorySeparatorChar, [char]0x2f)
}

<# Returns a stable repository-relative path using forward slashes. #>
function Get-RepositoryRelativePath {
    param([Parameter(Mandatory)][string] $RepositoryRoot, [Parameter(Mandatory)][string] $Path)
    $relative = [IO.Path]::GetRelativePath($RepositoryRoot, [IO.Path]::GetFullPath($Path))
    return ConvertTo-RepositoryPath $relative
}
