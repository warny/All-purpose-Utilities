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
    'unit-tests' = 25
    'functional-tests' = 25
    'canonical-packages' = 30
    'packaged-validation-ubuntu' = 25
    'packaged-validation-windows-light' = 25
    'source-gates-ubuntu' = 25
    required = 5
}
Assert-WorkflowTimeouts '.github/workflows/release-quality-gates.yml' @{
    canonical = 90
    'packaged-validation' = 120
    'source-gates' = 45
    reproducibility = 45
    candidate = 15
}

$pullRequestWorkflow = Get-Content (Join-Path $repoRoot '.github/workflows/dotnetcore.yml') -Raw
if ($pullRequestWorkflow -notmatch "github\.event_name.*workflow_dispatch" -or $pullRequestWorkflow -notmatch 'get-validation-scope\.ps1 -ForceProductTrain') {
    throw 'Manual pull-request workflow dispatch does not explicitly force the complete product train.'
}
foreach ($forbidden in @('assemble-pr-candidate', 'assemble-validated-product-train.ps1', 'publish-product-train.ps1', 'full-product-train-')) {
    if ($pullRequestWorkflow.Contains($forbidden)) { throw "Pull-request workflow contains publication concern '$forbidden'." }
}
foreach ($requiredDependency in @('build', 'unit-tests', 'functional-tests', 'canonical-packages', 'packaged-validation-ubuntu', 'packaged-validation-windows-light', 'source-gates-ubuntu')) {
    if ($pullRequestWorkflow -notmatch [regex]::Escape("needs.$requiredDependency.result")) {
        throw "Required job does not enforce '$requiredDependency'."
    }
}
foreach ($parallelRoot in @('build', 'unit-tests', 'functional-tests', 'canonical-packages')) {
    if ($pullRequestWorkflow -notmatch "(?ms)^    $([regex]::Escape($parallelRoot)):\s+needs: changes\s") {
        throw "Pull-request job '$parallelRoot' is not released directly by changes."
    }
}
foreach ($canonicalDependent in @('packaged-validation-ubuntu', 'packaged-validation-windows-light', 'source-gates-ubuntu')) {
    if ($pullRequestWorkflow -notmatch "(?ms)^    $([regex]::Escape($canonicalDependent)):\s+needs: canonical-packages\s") {
        throw "Pull-request job '$canonicalDependent' does not start directly after canonical packaging."
    }
}
if ($pullRequestWorkflow -notmatch '(?ms)name: Write measured phase summary\s+if: always\(\)\s+continue-on-error: true') {
    throw 'Workflow timing collection is not explicitly best-effort.'
}
$fullReleaseWorkflow = Get-Content (Join-Path $repoRoot '.github/workflows/release-quality-gates.yml') -Raw
foreach ($required in @('assemble-validated-product-train.ps1', 'publish-product-train.ps1', 'full-product-train-${{ github.sha }}')) {
    if (-not $fullReleaseWorkflow.Contains($required)) { throw "FullRelease workflow lost '$required'." }
}
Write-Host 'Workflow timeout tests passed.'
