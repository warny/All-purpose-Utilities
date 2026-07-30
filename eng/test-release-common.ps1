<#
.SYNOPSIS
Validates cross-platform behavior of the shared release helpers.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Release.Common.ps1")

$pathCases = [ordered]@{
    'Utils\Utils.csproj' = 'Utils/Utils.csproj'
    'Utils/Utils.csproj' = 'Utils/Utils.csproj'
    'Utils\Nested/Utils.csproj' = 'Utils/Nested/Utils.csproj'
}
foreach ($case in $pathCases.GetEnumerator()) {
    $actual = ConvertTo-RepositoryPath $case.Key
    if ($actual -cne $case.Value) {
        throw "Path normalization failed for '$($case.Key)': expected '$($case.Value)', got '$actual'."
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "Utils/Utils.csproj"
$relative = Get-RepositoryRelativePath $repoRoot $project
if ($relative -cne 'Utils/Utils.csproj') {
    throw "Repository-relative path normalization failed: got '$relative'."
}

$fingerprintRoot = Join-Path ([IO.Path]::GetTempPath()) "release-fingerprint-$([guid]::NewGuid().ToString('N'))"
try {
    $first = Join-Path $fingerprintRoot "first"
    $second = Join-Path $fingerprintRoot "second"
    New-Item (Join-Path $first "nested") -ItemType Directory -Force | Out-Null
    New-Item (Join-Path $second "nested") -ItemType Directory -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $first "nested/B.g.cs"), "second")
    [IO.File]::WriteAllText((Join-Path $first "A.g.cs"), "first")
    [IO.File]::WriteAllText((Join-Path $second "A.g.cs"), "first")
    [IO.File]::WriteAllText((Join-Path $second "nested/B.g.cs"), "second")
    $firstHash = Get-GeneratedOutputFingerprint $first
    $secondHash = Get-GeneratedOutputFingerprint $second
    if ($firstHash -cne $secondHash) {
        throw "Generated-output fingerprints depend on file enumeration order or root path."
    }
    [IO.File]::WriteAllText((Join-Path $second "nested/B.g.cs"), "changed")
    if ((Get-GeneratedOutputFingerprint $second) -ceq $firstHash) {
        throw "Generated-output fingerprint did not detect a content change."
    }
} finally {
    Remove-Item $fingerprintRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Release common helper tests passed."

$nativeRoot = Join-Path ([IO.Path]::GetTempPath()) "release-native-$([guid]::NewGuid().ToString('N'))"
try {
    New-Item $nativeRoot -ItemType Directory -Force | Out-Null
    $pwsh = Join-Path $PSHOME $(if ($IsWindows) { "pwsh.exe" } else { "pwsh" })
    $success = Invoke-NativeCommand -FilePath $pwsh -ArgumentList @("-NoProfile", "-Command", "[Console]::Out.Write('hello output'); [Console]::Error.Write('hello error')") -Timeout ([TimeSpan]::FromSeconds(10)) -LogPath (Join-Path $nativeRoot "success.log")
    if ($success.ExitCode -ne 0 -or $success.StandardOutput -notmatch "hello output" -or $success.StandardError -notmatch "hello error") { throw "Native stdout/stderr capture failed." }

    $spaced = Join-Path $nativeRoot "argument with spaces.txt"
    $argumentScript = Join-Path $nativeRoot "echo-argument.ps1"
    Set-Content -LiteralPath $argumentScript -Value 'param([string] $Value) [Console]::Write($Value)'
    $argumentResult = Invoke-NativeCommand -FilePath $pwsh -ArgumentList @("-NoProfile", "-File", $argumentScript, $spaced) -Timeout ([TimeSpan]::FromSeconds(10)) -LogPath (Join-Path $nativeRoot "spaces.log")
    if ($argumentResult.StandardOutput -cne $spaced) { throw "A native argument containing spaces was not preserved." }

    $failed = Invoke-NativeCommand -FilePath $pwsh -ArgumentList @("-NoProfile", "-Command", "exit 7") -Timeout ([TimeSpan]::FromSeconds(10)) -LogPath (Join-Path $nativeRoot "failure.log") -IgnoreExitCode
    if ($failed.ExitCode -ne 7) { throw "Non-zero native exit code was not returned." }
    try { Invoke-NativeCommand -FilePath $pwsh -ArgumentList @("-NoProfile", "-Command", "exit 8") -Timeout ([TimeSpan]::FromSeconds(10)) -LogPath (Join-Path $nativeRoot "throw.log") | Out-Null; throw "Non-zero command did not throw." } catch { if ($_.Exception.Message -notmatch "exit code: 8") { throw } }

    $sentinel = Join-Path $nativeRoot "child-survived.txt"
    $childScript = "Start-Sleep -Seconds 4; Set-Content -LiteralPath '$($sentinel.Replace("'", "''"))' survived"
    $parentScript = "Start-Process -FilePath '$($pwsh.Replace("'", "''"))' -ArgumentList @('-NoProfile','-Command',`"$childScript`"); Start-Sleep -Seconds 30"
    try { Invoke-NativeCommand -FilePath $pwsh -ArgumentList @("-NoProfile", "-Command", $parentScript) -Timeout ([TimeSpan]::FromSeconds(1)) -LogPath (Join-Path $nativeRoot "timeout.log") | Out-Null; throw "Timed command did not throw." } catch { if ($_.Exception.Message -notmatch "timed out") { throw } }
    Start-Sleep -Seconds 5
    if (Test-Path $sentinel) { throw "Timed command left a child process running." }
} finally {
    Remove-Item $nativeRoot -Recurse -Force -ErrorAction SilentlyContinue
}
