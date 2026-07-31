<# .SYNOPSIS Converts GitHub Actions job timestamps into the release timing report. #>
[CmdletBinding()]
param([Parameter(Mandatory)][string] $JobsJson, [string] $ArtifactsPath = 'artifacts')
$jobs = @((Get-Content $JobsJson -Raw | ConvertFrom-Json).jobs)
$mapping = [ordered]@{
    'Build' = @('build')
    'Unit tests' = @('unit-tests')
    'Functional tests' = @('functional-tests')
    'Canonical packaging' = @('canonical-packages', 'Produce canonical packages (Ubuntu)')
    'Ubuntu packaged validation' = @('packaged-validation-ubuntu')
    'Windows packaged validation' = @('packaged-validation-windows-light')
    'Source gates' = @('source-gates-ubuntu')
}
$records = foreach ($entry in $mapping.GetEnumerator()) {
    $job = $jobs | Where-Object name -in $entry.Value | Select-Object -First 1
    if ($job -and $job.started_at -and $job.completed_at) {
        $start = [DateTimeOffset]$job.started_at
        $end = [DateTimeOffset]$job.completed_at
        [ordered]@{ phase = $entry.Key; startedAtUtc = $start.ToString('O'); endedAtUtc = $end.ToString('O'); durationSeconds = ($end - $start).TotalSeconds; conclusion = $job.conclusion }
    }
}
$root = Join-Path (Split-Path -Parent $PSScriptRoot) $ArtifactsPath
New-Item (Join-Path $root 'reports') -ItemType Directory -Force | Out-Null
[ordered]@{ generatedAtUtc = [DateTime]::UtcNow.ToString('O'); phases = @($records) } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root 'reports/workflow-timings.json')
if ($env:GITHUB_STEP_SUMMARY) {
    "## Workflow timings`n`n| Phase | Duration | Result |`n|---|---:|---|" | Add-Content $env:GITHUB_STEP_SUMMARY
    foreach ($record in $records) { "| $($record.phase) | $([TimeSpan]::FromSeconds($record.durationSeconds)) | $($record.conclusion) |" | Add-Content $env:GITHUB_STEP_SUMMARY }
}
