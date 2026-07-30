<#
.SYNOPSIS
Validates release-gate mode, skip behavior, and ordering without network access.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"
$orchestrator = Join-Path $PSScriptRoot "run-release-quality-gates.ps1"
$prPlan = @(& $orchestrator -Mode PullRequest -PlanOnly 6>&1 | ForEach-Object ToString)
foreach ($skipped in @("build", "tests", "reproducibility")) {
    if ($prPlan -notcontains "SKIP $skipped") { throw "PullRequest mode did not skip '$skipped'." }
}
$expected = @("RUN package-discovery", "RUN packaged-product-train", "RUN api-compatibility", "RUN release-warnings", "RUN sourcelink", "RUN dependency-audit", "RUN release-manifest")
$actual = @($prPlan | Where-Object { $_ -like "RUN *" })
if (($actual -join "|") -cne ($expected -join "|")) { throw "PullRequest gates ran in an unexpected order: $($actual -join ', ')." }
$fullPlan = @(& $orchestrator -Mode FullRelease -SkipPackagedAcceptance -SkipRemoteSourceLink -SkipOutdatedDependencyAudit -PlanOnly 6>&1 | ForEach-Object ToString)
if ($fullPlan -notcontains "SKIP packaged-product-train") { throw "Explicit packaged-acceptance skip was ignored." }
if ($fullPlan -notcontains "RUN build" -or $fullPlan -notcontains "RUN tests" -or $fullPlan -notcontains "RUN reproducibility") { throw "FullRelease mode omitted a required gate." }
Write-Host "Release orchestrator tests passed."

$audit = Join-Path $PSScriptRoot "audit-dependencies.ps1"
$prAuditPlan = @(& $audit -SkipDeprecated -SkipOutdated -PlanOnly 6>&1 | ForEach-Object ToString)
if (($prAuditPlan -join "|") -cne "AUDIT vulnerable") {
    throw "The PR dependency audit must run only the complete vulnerable mode, got: $($prAuditPlan -join ', ')."
}
$fullAuditPlan = @(& $audit -PlanOnly 6>&1 | ForEach-Object ToString)
if (($fullAuditPlan -join "|") -cne "AUDIT vulnerable|AUDIT deprecated|AUDIT outdated") {
    throw "Full dependency audits were flattened or ordered incorrectly: $($fullAuditPlan -join ', ')."
}
