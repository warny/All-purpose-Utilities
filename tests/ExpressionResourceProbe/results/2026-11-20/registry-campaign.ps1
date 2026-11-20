param(
    [Parameter(Mandatory = $true)][string]$Probe,
    [Parameter(Mandatory = $true)][string]$Output,
    [int]$Repeat = 7,
    [switch]$SkipTiming
)

$ErrorActionPreference = 'Stop'

# name, size, extra arguments
$scenarios = @(
    @('csyntax-sum', 1000, @()),
    @('csyntax-sum', 5000, @()),
    @('csyntax-mixed', 1000, @()),
    @('vbsyntax-sum', 1000, @()),
    @('vbsyntax-sum', 5000, @()),
    @('vbsyntax-mixed', 1000, @()),
    @('csyntax-parens', 15, @()),
    @('g-alternatives', 2000, @()),
    @('g-prefix', 2000, @()),
    @('g-ambiguous', 150, @('--thread', 'stack:262144')),
    @('g-leftrec-sum', 2000, @()),
    @('g-leftrec-mixed', 1000, @()),
    @('g-deep', 300, @('--thread', 'stack:262144')),
    @('g-stateful', 1000, @())
)

$lines = New-Object System.Collections.Generic.List[string]
foreach ($scenario in $scenarios) {
    $name = $scenario[0]
    $size = [string]$scenario[1]
    $extra = $scenario[2]
    foreach ($mode in @(@('--peak'), @('--peak', '--diagnostics'))) {
        $arguments = @('parser-registry', $name, $size) + $mode + $extra
        $result = & $Probe @arguments
        $lines.Add("$LASTEXITCODE $result")
    }

    if (-not $SkipTiming) {
        $arguments = @('parser-registry', $name, $size, '--repeat', [string]$Repeat) + $extra
        $result = & $Probe @arguments
        $lines.Add("$LASTEXITCODE $result")
    }
}

# S-06 refusals near the real thresholds (retained totals measured on the baseline) and far below them.
$refusals = @(
    @('csyntax-sum', 1000, @(150279, 150278, 170301, 170300, 30000, 1000, 1)),
    @('vbsyntax-sum', 1000, @(119165, 119164, 141169, 141168, 47765, 20000)),
    @('g-stateful', 1000, @(26689, 26688, 25354, 25353, 5000, 500)),
    @('g-leftrec-sum', 2000, @(15999, 15998, 7000)),
    @('csyntax-mixed', 1000, @(682265, 682264, 747429, 747428, 40000, 10000))
)
foreach ($refusal in $refusals) {
    foreach ($limit in $refusal[2]) {
        foreach ($mode in @(@('--peak'), @('--peak', '--diagnostics'))) {
            $arguments = @('parser-registry', $refusal[0], [string]$refusal[1], '--max-retained', [string]$limit) + $mode
            $result = & $Probe @arguments
            $lines.Add("$LASTEXITCODE $result")
        }
    }
}

Set-Content -Path $Output -Value $lines -Encoding UTF8
