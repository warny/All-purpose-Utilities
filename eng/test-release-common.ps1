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
