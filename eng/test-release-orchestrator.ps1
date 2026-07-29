<#
.SYNOPSIS
Validates orchestration behavior for in-process PowerShell release gates.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"
$previousExitCode = $global:LASTEXITCODE
try {
    # Simulate a failed native command that ran before a successful PowerShell gate.
    & dotnet definitely-invalid-command *> $null
    if ($global:LASTEXITCODE -eq 0) {
        throw "The native-command failure setup unexpectedly succeeded."
    }
    $residualExitCode = $global:LASTEXITCODE
    & (Join-Path $PSScriptRoot "test-release-common.ps1")
    if (-not $?) {
        throw "A successful PowerShell release gate reported failure."
    }
    if ($global:LASTEXITCODE -ne $residualExitCode) {
        throw "The regression setup did not preserve the residual native exit code."
    }
} finally {
    $global:LASTEXITCODE = $previousExitCode
}

Write-Host "Release orchestrator tests passed."
