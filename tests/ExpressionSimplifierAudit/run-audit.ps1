<#
.SYNOPSIS
    Runs the S5 deep-audit campaigns of ExpressionSimplifier and writes their results to a new, timestamped directory.

.DESCRIPTION
    Builds tests/ExpressionSimplifierAudit (Release) against the checkout given by -UtilsRoot, then runs each requested
    campaign in its own process. A campaign aborted by the harness watchdog (one case over -CaseTimeoutSeconds, or the managed
    heap over -MemoryMB) is resumed after the aborted case; the aborted cases stay listed in <campaign>-aborted.txt.
    The complexity campaign measures each family at each size in a fresh process and stops a family after the first size
    whose median exceeds -StopAfterSeconds, that is refused, that times out or that crashes, so exponential families are never
    run blindly at large sizes. Existing results are never overwritten.

    Exit code: 0 when no campaign reported an anomaly, 1 when at least one did, 3 on a harness or build failure.

.EXAMPLE
    .\tests\ExpressionSimplifierAudit\run-audit.ps1 -Campaigns terminate,converge -Seeds 2000

.EXAMPLE
    .\tests\ExpressionSimplifierAudit\run-audit.ps1 -Campaigns complexity -Families p,g,F -Sizes 4,6,8,10,12 -Unlimited
#>
[CmdletBinding()]
param(
    [string] $UtilsRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string] $OutputRoot = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'artifacts\s5-audit'),
    [ValidateSet('inventory', 'terminate', 'converge', 'canon', 'comparer', 'diff', 'effects', 's3', 'complexity')]
    [string[]] $Campaigns = @('terminate', 'inventory', 'converge', 'canon', 'comparer', 'diff', 'effects', 's3', 'complexity'),
    [int] $Seeds = 4000,
    [int] $Nested = 4000,
    [int] $Reduce = 40,
    [string] $Types = 'double,float,int,long,decimal,int?,uint',
    [int] $CaseTimeoutSeconds = 60,
    [int] $MemoryMB = 6144,
    [string[]] $Families = @('p', 'B', 'g', 'F', 'z', 'e', 'k', 'n', 'o', 'r', 'H', 'Lsub', 'Rsub', 'Lmul', 'Gm', 'div', 'pow', 'bal', 'negmul', 'negdiv', 'subneg', 'negsum', 'zerominus', 'plus0', 'zeroplus', 'pow1'),
    [int[]] $Sizes = @(4, 6, 8, 10, 12, 14, 16, 18, 20),
    [int] $MeasureTimeoutSeconds = 120,
    [double] $StopAfterSeconds = 20,
    [switch] $Unlimited,
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'ExpressionSimplifierAudit.csproj'
$root = $UtilsRoot.TrimEnd('\') + '\'

if (-not $NoBuild) {
    $buildArguments = @('build', $project, '--configuration', 'Release', "-p:AuditUtilsRoot=$root", '--nologo', '--verbosity', 'quiet')
    & dotnet @buildArguments
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet build failed with exit code $LASTEXITCODE."
        exit 3
    }
}

$exe = Join-Path $PSScriptRoot 'bin\Release\net9.0\ExpressionSimplifierAudit.exe'
if (-not (Test-Path $exe)) {
    Write-Error "Harness not found: $exe"
    exit 3
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$output = Join-Path $OutputRoot $stamp
$suffix = 1
while (Test-Path $output) {
    $output = Join-Path $OutputRoot ($stamp + '-' + $suffix)
    $suffix++
}
New-Item -ItemType Directory -Force -Path $output | Out-Null

$commit = (& git -C $UtilsRoot rev-parse HEAD) 2>$null
$environment = @(
    "commit: $commit"
    "date: $(Get-Date -Format o)"
    "machine: $env:COMPUTERNAME, $((Get-CimInstance Win32_Processor | Select-Object -First 1).Name), $([Environment]::ProcessorCount) logical processors"
    "os: $([Environment]::OSVersion.VersionString)"
    "dotnet: $(& dotnet --version)"
    "parameters: Seeds=$Seeds Nested=$Nested Types=$Types CaseTimeout=$CaseTimeoutSeconds MemoryMB=$MemoryMB Unlimited=$Unlimited"
)
Set-Content -Path (Join-Path $output 'environment.txt') -Value $environment -Encoding UTF8

$anomalies = $false
$failure = $false

function Invoke-Campaign {
    param([string] $Name)

    $start = 0
    $resume = $false
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        $arguments = @($Name, '--out', $output, '--seeds', $Seeds, '--nested', $Nested, '--reduce', $Reduce, '--types', $Types,
            '--timeout', $CaseTimeoutSeconds, '--memory', $MemoryMB, '--start', $start)
        if ($resume) { $arguments += '--resume' }
        # Windows PowerShell 5.1 turns the harness's stderr lines into error records: keep them non-terminating here.
        $ErrorActionPreference = 'Continue'
        & $exe @arguments 2>&1 | ForEach-Object { Write-Host $_ }
        $code = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        if ($code -eq 5 -or $code -eq 6) {
            $aborted = Get-Content (Join-Path $output "$Name-aborted.txt") | Select-Object -Last 1
            if ($aborted -notmatch '#(\d+)') {
                Write-Warning "$Name aborted without a resumable case index: $aborted"
                return 3
            }
            $start = [int]$Matches[1] + 1
            $resume = $true
            Write-Warning "$Name aborted ($aborted); resuming at $start"
            continue
        }
        return $code
    }
    return 3
}

foreach ($campaign in $Campaigns) {
    if ($campaign -eq 'complexity') { continue }
    Write-Host "== $campaign"
    $code = Invoke-Campaign -Name $campaign
    if ($code -eq 1) { $anomalies = $true }
    elseif ($code -ne 0) {
        Write-Warning "$campaign failed with exit code $code"
        $failure = $true
    }
}

if ($Campaigns -contains 'complexity') {
    Write-Host '== complexity'
    $measures = Join-Path $output 'measure.jsonl'
    foreach ($family in $Families) {
        foreach ($size in $Sizes) {
            $arguments = @('measure', $family, $size, '--out', $output, '--timeout', $MeasureTimeoutSeconds, '--memory', $MemoryMB)
            if ($Unlimited) { $arguments += '--unlimited' }
            $ErrorActionPreference = 'Continue'
            $line = & $exe @arguments 2>&1 | ForEach-Object { "$_" } | Select-Object -Last 1
            $code = $LASTEXITCODE
            $ErrorActionPreference = 'Stop'
            if ($code -ne 0) {
                $reason = switch ($code) { 5 { 'timeout' } 6 { 'memory' } default { "crash:$code" } }
                $text = '{"family":"' + $family + '","n":' + $size + ',"outcome":"' + $reason + '"}' + [Environment]::NewLine
                [System.IO.File]::AppendAllText($measures, $text, (New-Object System.Text.UTF8Encoding($false)))
                Write-Warning "$family $size : $reason; family stopped"
                break
            }
            $record = $line | ConvertFrom-Json
            Write-Host ("{0,-5} {1,3} {2,-28} {3,12} ms {4,12} visits {5,10} comparer" -f $family, $size, $record.outcome, $record.medianMs, $record.visits, $record.comparerSimplifications)
            if ($record.outcome -ne 'ok' -or ($record.medianMs / 1000) -gt $StopAfterSeconds) {
                Write-Host "$family stopped after n = $size"
                break
            }
        }
    }
}

Write-Host "Results: $output"
if ($failure) { exit 3 }
if ($anomalies) { exit 1 }
exit 0
