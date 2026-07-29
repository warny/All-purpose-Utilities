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

Write-Host "Release common helper tests passed."
