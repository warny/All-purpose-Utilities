<#
.SYNOPSIS
    Bounded probes of the multiplicative DAG families (mission "multiplicative DAG resource bounds").

.DESCRIPTION
    Runs `dagprobe <family> <n>` for each family and size, each point in a fresh process. Each process is bounded twice: by
    the harness watchdog (--timeout, --memory: exit codes 5 and 6) and by this script, which kills it after
    TimeoutSeconds + 15 s. A family stops after its first point that times out, exceeds the memory limit or is killed. The
    records are appended to <Out>\dagprobe.jsonl as they come (intermediate results survive a kill), and the aborted points to
    <Out>\dagprobe-aborted.txt.

.EXAMPLE
    .\tests\ExpressionSimplifierAudit\run-dagprobe.ps1 -Families mdag,mdagq -Sizes 4,8,12,16 -TimeoutSeconds 60
#>
param(
    [string[]] $Families = @('mdag', 'mdagtree', 'mdagq', 'mdagm4', 'mdag2', 'mdagmix', 'mdagnone', 'mdagopaque', 'mdagieee', 'mdagnan', 'mdaglong', 'mdagcustom'),
    [int[]] $Sizes = @(4, 8, 12, 16, 20),
    [int] $TimeoutSeconds = 60,
    [int] $MemoryMB = 4096,
    [string] $Out = '',
    [string] $Exe = ''
)

$ErrorActionPreference = 'Stop'
if ($Exe -eq '') { $Exe = Join-Path $PSScriptRoot 'bin\Release\net9.0\ExpressionSimplifierAudit.exe' }
if (-not (Test-Path $Exe)) { throw "Harness not built: $Exe" }
if ($Out -eq '') { $Out = Join-Path $PSScriptRoot ('artifacts\dagprobe\' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$aborted = Join-Path $Out 'dagprobe-aborted.txt'

foreach ($family in $Families) {
    foreach ($size in $Sizes) {
        if ($family -eq 'mdagtree' -and $size -gt 16) { continue }
        $arguments = @('dagprobe', $family, $size, '--out', ('"' + $Out + '"'), '--timeout', $TimeoutSeconds, '--memory', $MemoryMB)
        $log = Join-Path $Out ("$family-$size.log")
        $process = Start-Process -FilePath $Exe -ArgumentList $arguments -NoNewWindow -PassThru -RedirectStandardOutput $log -RedirectStandardError ($log + '.err')

        # Windows PowerShell 5.1 only reports the exit code of a process whose handle was opened before it exits.
        $null = $process.Handle
        $finished = $process.WaitForExit(($TimeoutSeconds + 15) * 1000)
        if (-not $finished) {
            try { $process.Kill() } catch { Write-Warning "Kill failed: $($_.Exception.Message)" }
            [System.IO.File]::AppendAllText($aborted, "$family $size killed`r`n")
            Write-Host "$family $size : killed after $($TimeoutSeconds + 15) s"
            break
        }

        $code = $process.ExitCode
        if ($code -eq 5 -or $code -eq 6) {
            Write-Host "$family $size : aborted by the watchdog (exit $code)"
            break
        }

        if ($code -ne 0) {
            [System.IO.File]::AppendAllText($aborted, "$family $size exit $code`r`n")
            Write-Host "$family $size : exit $code"
            break
        }
    }
}

Write-Host "Results in $Out"
