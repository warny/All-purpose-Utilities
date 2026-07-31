<# .SYNOPSIS Verifies that release workflow jobs retain bounded execution times. #>
[CmdletBinding()] param()
$repoRoot = Split-Path -Parent $PSScriptRoot

<# Reads one workflow and verifies the timeout declared by each named job. #>
function Assert-WorkflowTimeouts {
    param([Parameter(Mandatory)][string] $Path, [Parameter(Mandatory)][hashtable] $Expected)
    $lines = Get-Content (Join-Path $repoRoot $Path)
    foreach ($entry in $Expected.GetEnumerator()) {
        $jobLine = $lines | Select-String "^    $([regex]::Escape($entry.Key)):$" | Select-Object -First 1
        if (-not $jobLine) { throw "Workflow '$Path' does not contain job '$($entry.Key)'." }
        $following = @($lines | Select-Object -Skip $jobLine.LineNumber -First 12)
        if (-not ($following -match "^        timeout-minutes: $($entry.Value)$")) {
            throw "Workflow '$Path' job '$($entry.Key)' does not retain timeout $($entry.Value)."
        }
    }
}

Assert-WorkflowTimeouts '.github/workflows/dotnetcore.yml' @{
    changes = 5
    build = 20
    tests = 25
    'canonical-packages' = 30
    'packaged-validation' = 35
    'source-gates' = 30
    'assemble-pr-candidate' = 10
    required = 5
}
Assert-WorkflowTimeouts '.github/workflows/release-quality-gates.yml' @{
    canonical = 90
    'packaged-validation' = 120
    'source-gates' = 45
    reproducibility = 45
    candidate = 15
}
Write-Host 'Workflow timeout tests passed.'
