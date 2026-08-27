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
        [TimeSpan] $TerminationTimeout = ([TimeSpan]::FromSeconds(5)),
        [switch] $IgnoreExitCode
    )

    if ($Timeout -le [TimeSpan]::Zero) { throw "Timeout must be greater than zero." }
    if ($TerminationTimeout -le [TimeSpan]::Zero) { throw "TerminationTimeout must be greater than zero." }
    $resolvedLog = [IO.Path]::GetFullPath($LogPath)
    New-Item (Split-Path $resolvedLog -Parent) -ItemType Directory -Force | Out-Null
    $displayCommand = (@($FilePath) + @($ArgumentList | ForEach-Object { if ($_ -match '\s') { '"' + $_.Replace('"', '\"') + '"' } else { $_ } })) -join ' '
    $start = [DateTime]::UtcNow
    $stdout = [Text.StringBuilder]::new()
    $stderr = [Text.StringBuilder]::new()
    $status = "FAILED"
    $exitCode = $null
    $processStarted = $false
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = [Diagnostics.ProcessStartInfo]::new()
    $process.StartInfo.FileName = $FilePath
    $process.StartInfo.WorkingDirectory = $WorkingDirectory
    $process.StartInfo.UseShellExecute = $false
    $process.StartInfo.RedirectStandardOutput = $true
    $process.StartInfo.RedirectStandardError = $true
    $process.StartInfo.CreateNoWindow = $true
    foreach ($argument in $ArgumentList) { [void]$process.StartInfo.ArgumentList.Add($argument) }

    $logWriter = [IO.StreamWriter]::new($resolvedLog, $false, [Text.UTF8Encoding]::new($false))
    $logWriter.AutoFlush = $true
    $startMessage = "[$($start.ToString('O'))] COMMAND START: $displayCommand"
    Write-Host $startMessage
    Write-Host "Log: $resolvedLog"
    $logWriter.WriteLine($startMessage)
    $logWriter.WriteLine("WORKING DIRECTORY: $WorkingDirectory")
    try {
        if (-not $process.Start()) { throw "Unable to start native command: $displayCommand" }
        $processStarted = $true
        $stdoutTask = $process.StandardOutput.ReadLineAsync()
        $stderrTask = $process.StandardError.ReadLineAsync()
        $stdoutComplete = $false
        $stderrComplete = $false
        $timedOut = $false
        $exitObservedAt = $null

        while (-not ($process.HasExited -and $stdoutComplete -and $stderrComplete)) {
            if ($process.HasExited -and $null -eq $exitObservedAt) { $exitObservedAt = [DateTime]::UtcNow }
            if (-not $stdoutComplete -and $stdoutTask.IsCompleted) {
                $line = $stdoutTask.GetAwaiter().GetResult()
                if ($null -eq $line) { $stdoutComplete = $true }
                else {
                    [void]$stdout.AppendLine($line)
                    Write-Host $line
                    $logWriter.WriteLine("[stdout] $line")
                    $stdoutTask = $process.StandardOutput.ReadLineAsync()
                }
            }
            if (-not $stderrComplete -and $stderrTask.IsCompleted) {
                $line = $stderrTask.GetAwaiter().GetResult()
                if ($null -eq $line) { $stderrComplete = $true }
                else {
                    [void]$stderr.AppendLine($line)
                    [Console]::Error.WriteLine($line)
                    $logWriter.WriteLine("[stderr] $line")
                    $stderrTask = $process.StandardError.ReadLineAsync()
                }
            }
            if (-not $process.HasExited -and ([DateTime]::UtcNow - $start) -ge $Timeout) {
                $timedOut = $true
                $status = "TIMEOUT"
                $logWriter.WriteLine("TIMEOUT reached after $Timeout; terminating process tree.")
                try { $process.Kill($true) }
                catch {
                    $message = "Unable to terminate the complete process tree: $($_.Exception.Message)"
                    Write-Warning $message
                    $logWriter.WriteLine("KILL ERROR: $message")
                }
                $terminationMilliseconds = [int][Math]::Min($TerminationTimeout.TotalMilliseconds, [int]::MaxValue)
                if (-not $process.WaitForExit($terminationMilliseconds)) {
                    throw "Native command timed out and did not terminate within $TerminationTimeout after the kill attempt: $displayCommand. Log: $resolvedLog"
                }
            }
            if ($null -ne $exitObservedAt -and ([DateTime]::UtcNow - $exitObservedAt) -ge $TerminationTimeout) {
                $logWriter.WriteLine("STREAM DRAIN TIMEOUT: redirected streams did not close within $TerminationTimeout after process exit.")
                break
            }
            if (-not ($process.HasExited -and $stdoutComplete -and $stderrComplete)) { Start-Sleep -Milliseconds 20 }
        }

        $exitCode = $process.ExitCode
        $duration = [DateTime]::UtcNow - $start
        if ($timedOut) { throw "Native command timed out after $duration (exit code: $exitCode): $displayCommand. Log: $resolvedLog" }
        $status = if ($exitCode -eq 0) { "SUCCESS" } else { "FAILED" }
        $result = [pscustomobject]@{ ExitCode = $exitCode; StandardOutput = $stdout.ToString().TrimEnd(); StandardError = $stderr.ToString().TrimEnd(); Duration = $duration; LogPath = $resolvedLog; Command = $displayCommand }
        if ($exitCode -ne 0 -and -not $IgnoreExitCode) { throw "Native command failed after $duration (exit code: $exitCode): $displayCommand. Log: $resolvedLog" }
        return $result
    } finally {
        $duration = [DateTime]::UtcNow - $start
        if ($processStarted -and $process.HasExited) { $exitCode = $process.ExitCode }
        $endMessage = "[$([DateTime]::UtcNow.ToString('O'))] COMMAND END — $status — $duration — exit code: $(if ($null -eq $exitCode) { 'unavailable' } else { $exitCode })"
        Write-Host $endMessage
        $logWriter.WriteLine($endMessage)
        $logWriter.Dispose()
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

<#
Derives the four-part AssemblyVersion/FileVersion the .NET SDK generates from a NuGet <Version>
when no AssemblyVersion/FileVersion override is present: the prerelease suffix is dropped, the
three remaining numeric parts are kept, and a trailing ".0" revision is appended.
#>
function Get-PackageAssemblyVersion {
    param([Parameter(Mandatory)][string] $Version)
    $core = $Version.Split('-', 2)[0]
    $parts = @($core.Split('.'))
    while ($parts.Count -lt 3) { $parts += "0" }
    return "{0}.{1}.{2}.0" -f $parts[0], $parts[1], $parts[2]
}
