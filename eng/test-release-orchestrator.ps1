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

$packagedScript = Get-Content (Join-Path $PSScriptRoot 'test-packaged-product-train.ps1') -Raw
if ($packagedScript -notmatch 'get-packaged-validation-plan\.ps1' -or $packagedScript -notmatch '\$validationPlan\.perConsumerVulnerabilityAudit') {
    throw 'Packaged validation does not consume the tested tier plan or guard per-consumer vulnerability audits.'
}

& (Join-Path $PSScriptRoot 'test-validation-scope.ps1')
& (Join-Path $PSScriptRoot 'test-packaged-validation-plan.ps1')
& (Join-Path $PSScriptRoot 'test-workflow-timeouts.ps1')
& (Join-Path $PSScriptRoot 'test-publish-decision.ps1')
if (-not $?) { throw "Publication decision logic tests failed." }

$runnerArtifacts = Join-Path "artifacts" "packaged-runner-test-$([guid]::NewGuid().ToString('N'))"
try {
    & (Join-Path $PSScriptRoot "test-packaged-product-train.ps1") -ArtifactsPath $runnerArtifacts -TestNativeRunnerOnly
    if (-not $?) { throw "Packaged acceptance native runner test failed." }
} finally {
    Remove-Item (Join-Path (Split-Path -Parent $PSScriptRoot) $runnerArtifacts) -Recurse -Force -ErrorAction SilentlyContinue
}

& (Join-Path $PSScriptRoot "test-release-artifact-assembly.ps1")
if (-not $?) { throw "Release artifact assembly tests failed." }

& (Join-Path $PSScriptRoot "test-existing-package-validation.ps1")
if (-not $?) { throw "Existing canonical package validation tests failed." }

& (Join-Path $PSScriptRoot "test-release-warning-orchestration.ps1")
if (-not $?) { throw "Release warning orchestration tests failed." }

& (Join-Path $PSScriptRoot "test-release-project-discovery.ps1")
if (-not $?) { throw "Release project discovery tests failed." }
