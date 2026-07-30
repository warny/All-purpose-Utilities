<#
.SYNOPSIS
Validates autonomous and pre-restored release-warning execution plans without network access.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"
$script = Join-Path $PSScriptRoot "validate-release-warnings.ps1"
$autonomous = @(& $script -PlanOnly 6>&1 | ForEach-Object ToString)
if (($autonomous -join "|") -cne "RESTORE release-warnings|BUILD release-warnings --no-restore") {
    throw "The autonomous warning gate did not plan restore followed by a no-restore build."
}
$preRestored = @(& $script -NoRestore -PlanOnly 6>&1 | ForEach-Object ToString)
if (($preRestored -join "|") -cne "BUILD release-warnings --no-restore") {
    throw "NoRestore did not suppress only the release-warning restore."
}
Write-Host "Release warning orchestration tests passed."
