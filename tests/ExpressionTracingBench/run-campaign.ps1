param(
    # Checkout measured as the reference (for example a worktree of master): it must contain the tracing API.
    [Parameter(Mandatory = $true)]
    [string]$BaselineRoot,

    # Checkout measured as the candidate (by default, this checkout).
    [string]$CandidateRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,

    [int]$Rounds = 3,

    [string[]]$Scenarios = @('ext-root', 'scalar', 'binary', 'lambda', 'leftsum20', 'p9family16', 'dag10', 'block16', 'li1024', 'cmp-pos', 'hash'),

    # Also measures the candidate with a tracer (minimal and bounded collector).
    [switch]$WithTracer,

    [string]$Output = (Join-Path ([System.IO.Path]::GetTempPath()) 'etc-tracing-bench')
)

# Engine tracing benchmark campaign (Windows PowerShell 5.1). Builds the harness against the baseline and the candidate in
# separate copies (a shared obj directory would mix the two Utils references), adds an A/A copy of the baseline binaries,
# then runs every scenario once per variant and round in a rotating order and prints the medians relative to the baseline.
$ErrorActionPreference = 'Stop'

function Build-Variant([string]$Name, [string]$UtilsRoot) {
    $source = Join-Path $Output "src-$Name"
    $binaries = Join-Path $Output "bin-$Name"
    if (Test-Path $source) { Remove-Item $source -Recurse -Force }
    New-Item -ItemType Directory -Force $source | Out-Null
    Copy-Item (Join-Path $PSScriptRoot 'ExpressionTracingBench.csproj'), (Join-Path $PSScriptRoot 'Program.cs') $source
    $root = (Resolve-Path $UtilsRoot).Path.TrimEnd('\') + '\'
    $arguments = @(
        'build'
        (Join-Path $source 'ExpressionTracingBench.csproj')
        '--configuration'
        'Release'
        '--output'
        $binaries
        "-p:EtcUtilsRoot=$root"
    )
    & dotnet @arguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed for $Name with exit code $LASTEXITCODE." }
    return Join-Path $binaries 'ExpressionTracingBench.exe'
}

New-Item -ItemType Directory -Force $Output | Out-Null
$baselineExe = Build-Variant 'baseline' $BaselineRoot
$candidateExe = Build-Variant 'candidate' $CandidateRoot

# A/A: the same baseline binaries from another directory, to measure the noise of the machine.
$aaDirectory = Join-Path $Output 'bin-baseline-aa'
if (Test-Path $aaDirectory) { Remove-Item $aaDirectory -Recurse -Force }
Copy-Item (Join-Path $Output 'bin-baseline') $aaDirectory -Recurse
$aaExe = Join-Path $aaDirectory 'ExpressionTracingBench.exe'

$variants = @(
    @{ Name = 'baseline'; Exe = $baselineExe; Mode = 'off' },
    @{ Name = 'baseline-aa'; Exe = $aaExe; Mode = 'off' },
    @{ Name = 'candidate'; Exe = $candidateExe; Mode = 'off' }
)
if ($WithTracer) {
    $variants += @{ Name = 'candidate-minimal'; Exe = $candidateExe; Mode = 'minimal' }
    $variants += @{ Name = 'candidate-collector'; Exe = $candidateExe; Mode = 'collector' }
}

$results = Join-Path $Output 'results.csv'
[System.IO.File]::WriteAllText($results, "label,scenario,mode,medianNs,p25Ns,p75Ns,bytesPerOp,eventsPerOp,round`r`n")
for ($round = 0; $round -lt $Rounds; $round++) {
    for ($s = 0; $s -lt $Scenarios.Count; $s++) {
        for ($v = 0; $v -lt $variants.Count; $v++) {
            # Rotate the order per scenario and round, so no variant always runs first.
            $variant = $variants[($v + $round + $s) % $variants.Count]
            $line = & $variant.Exe $Scenarios[$s] $variant.Name $variant.Mode
            if ($LASTEXITCODE -ne 0) { throw "Benchmark failed: $($variant.Name) $($Scenarios[$s])." }
            [System.IO.File]::AppendAllText($results, "$line,$round`r`n")
        }
    }
}

$rows = Import-Csv $results
$culture = [System.Globalization.CultureInfo]::InvariantCulture
foreach ($scenario in $Scenarios) {
    $medians = @{}
    $bytes = @{}
    foreach ($variant in $variants) {
        $selected = @($rows | Where-Object { $_.scenario -eq $scenario -and $_.label -eq $variant.Name })
        $values = @($selected | ForEach-Object { [double]::Parse($_.medianNs, $culture) } | Sort-Object)
        $medians[$variant.Name] = $values[[int][Math]::Floor($values.Count / 2)]
        $bytes[$variant.Name] = $selected[0].bytesPerOp
    }

    $base = $medians['baseline']
    $line = '{0,-12} baseline {1,10:N1} ns {2,7} B   A/A {3,7:P1}   candidate {4,7:P1} ({5} B)' -f $scenario, $base, $bytes['baseline'], ($medians['baseline-aa'] / $base - 1), ($medians['candidate'] / $base - 1), $bytes['candidate']
    if ($WithTracer) {
        $line += '   minimal {0,7:P1}   collector {1,7:P1}' -f ($medians['candidate-minimal'] / $base - 1), ($medians['candidate-collector'] / $base - 1)
    }

    $line
}

"Raw results: $results"
