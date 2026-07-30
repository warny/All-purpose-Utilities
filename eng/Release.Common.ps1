<# Shared, side-effect-free helpers for the repository-wide release gates. #>

<#
.SYNOPSIS
Runs a native command with bounded execution time and durable diagnostics.
.DESCRIPTION
Arguments are added through ProcessStartInfo.ArgumentList so spaces and quoting are
preserved on Windows and Linux. Standard output and error are captured separately,
mirrored to the console, and written to a UTF-8 log. A timeout terminates the process
tree and throws an error containing the command, duration, exit code, and log path.
#>
function Invoke-NativeCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $FilePath,
        [string[]] $ArgumentList = @(),
        [TimeSpan] $Timeout = ([TimeSpan]::FromMinutes(10)),
        [Parameter(Mandatory)][string] $LogPath,
        [string] $WorkingDirectory = (Get-Location).Path,
        [switch] $IgnoreExitCode
    )

    if ($Timeout -le [TimeSpan]::Zero) { throw "Timeout must be greater than zero." }
    $resolvedLog = [IO.Path]::GetFullPath($LogPath)
    New-Item (Split-Path $resolvedLog -Parent) -ItemType Directory -Force | Out-Null
    $displayCommand = (@($FilePath) + @($ArgumentList | ForEach-Object { if ($_ -match '\s') { '"' + $_.Replace('"', '\"') + '"' } else { $_ } })) -join ' '
    $start = [DateTime]::UtcNow
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = [Diagnostics.ProcessStartInfo]::new()
    $process.StartInfo.FileName = $FilePath
    $process.StartInfo.WorkingDirectory = $WorkingDirectory
    $process.StartInfo.UseShellExecute = $false
    $process.StartInfo.RedirectStandardOutput = $true
    $process.StartInfo.RedirectStandardError = $true
    $process.StartInfo.CreateNoWindow = $true
    foreach ($argument in $ArgumentList) { [void]$process.StartInfo.ArgumentList.Add($argument) }
    try {
        if (-not $process.Start()) { throw "Unable to start native command: $displayCommand" }
        # Read both redirected streams concurrently to prevent a full pipe from
        # deadlocking the child while preserving stdout/stderr independently.
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit([int][Math]::Min($Timeout.TotalMilliseconds, [int]::MaxValue))) {
            try { $process.Kill($true) } catch { Write-Warning "Unable to terminate the complete process tree: $($_.Exception.Message)" }
            $process.WaitForExit()
            $stdoutText = $stdoutTask.GetAwaiter().GetResult()
            $stderrText = $stderrTask.GetAwaiter().GetResult()
            $duration = [DateTime]::UtcNow - $start
            $logText = "COMMAND: $displayCommand`nTIMEOUT: $Timeout`nDURATION: $duration`nSTDOUT:`n$stdoutText`nSTDERR:`n$stderrText"
            [IO.File]::WriteAllText($resolvedLog, $logText, [Text.UTF8Encoding]::new($false))
            throw "Native command timed out after $duration (exit code: timeout): $displayCommand. Log: $resolvedLog"
        }
        $process.WaitForExit()
        $stdoutText = $stdoutTask.GetAwaiter().GetResult()
        $stderrText = $stderrTask.GetAwaiter().GetResult()
        if ($stdoutText) { Write-Host $stdoutText.TrimEnd() }
        if ($stderrText) { [Console]::Error.WriteLine($stderrText.TrimEnd()) }
        $duration = [DateTime]::UtcNow - $start
        $exitCode = $process.ExitCode
        $logText = "COMMAND: $displayCommand`nEXIT CODE: $exitCode`nDURATION: $duration`nSTDOUT:`n$stdoutText`nSTDERR:`n$stderrText"
        [IO.File]::WriteAllText($resolvedLog, $logText, [Text.UTF8Encoding]::new($false))
        $result = [pscustomobject]@{ ExitCode = $exitCode; StandardOutput = $stdoutText; StandardError = $stderrText; Duration = $duration; LogPath = $resolvedLog; Command = $displayCommand }
        if ($exitCode -ne 0 -and -not $IgnoreExitCode) { throw "Native command failed after $duration (exit code: $exitCode): $displayCommand. Log: $resolvedLog" }
        return $result
    } finally {
        $process.Dispose()
    }
}

<# Runs a named release gate with timestamps, elapsed time, and GitHub log grouping. #>
function Invoke-ReleaseGate {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string] $Name, [Parameter(Mandatory)][string] $DisplayName, [Parameter(Mandatory)][scriptblock] $Action)
    $start = [DateTime]::UtcNow
    Write-Host "[$($start.ToString('O'))] START $Name"
    if ($env:GITHUB_ACTIONS -eq 'true') { Write-Host "::group::$DisplayName" }
    $succeeded = $false
    try {
        & $Action
        $succeeded = $true
    } catch {
        $duration = [DateTime]::UtcNow - $start
        throw "Gate '$Name' failed after $duration. $($_.Exception.Message)"
    } finally {
        $duration = [DateTime]::UtcNow - $start
        $status = if ($succeeded) { "SUCCESS" } else { "FAILED" }
        Write-Host "[$([DateTime]::UtcNow.ToString('O'))] END $Name — $duration — $status"
        if ($env:GITHUB_ACTIONS -eq 'true') { Write-Host "::endgroup::" }
    }
}

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
