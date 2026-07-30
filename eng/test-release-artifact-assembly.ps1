<#
.SYNOPSIS
Validates cross-platform candidate artifact assembly without network access.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$relativeRoot = "artifacts/assembly-test-$([guid]::NewGuid().ToString('N'))"
$root = Join-Path $repoRoot $relativeRoot
try {
    foreach ($platform in @("ubuntu", "windows")) {
        New-Item (Join-Path $root "$platform/packages") -ItemType Directory -Force | Out-Null
        New-Item (Join-Path $root "$platform/reports") -ItemType Directory -Force | Out-Null
        [IO.File]::WriteAllText((Join-Path $root "$platform/packages/candidate.nupkg"), "identical-candidate")
        [ordered]@{
            version = "test-version"
            passed = $true
            packages = @([ordered]@{ packageId = "test.package"; restored = $true; compiled = $true })
        } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $root "$platform/reports/packaged-acceptance.json")
    }
    New-Item (Join-Path $root "reproducibility/reports") -ItemType Directory -Force | Out-Null
    [ordered]@{
        artifacts = @([ordered]@{ packageId = "test.package"; result = "bit-identical" })
    } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $root "reproducibility/reports/reproducibility-report.json")

    & (Join-Path $PSScriptRoot "assemble-validated-product-train.ps1") -InputsPath $relativeRoot -ValidateInputsOnly
    [IO.File]::WriteAllText((Join-Path $root "windows/packages/candidate.nupkg"), "different-candidate")
    try {
        & (Join-Path $PSScriptRoot "assemble-validated-product-train.ps1") -InputsPath $relativeRoot -ValidateInputsOnly
        throw "Cross-platform package mismatch was not rejected."
    } catch {
        if ($_.Exception.Message -notmatch "differs between Ubuntu and Windows") { throw }
    }
} finally {
    Remove-Item $root -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "Release artifact assembly tests passed."
