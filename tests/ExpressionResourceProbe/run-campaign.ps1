<#
.SYNOPSIS
Runs the ETC R0 expression resource campaign: one isolated process per (scenario, operation, size).

.DESCRIPTION
Each run starts ExpressionResourceProbe in its own process, with a wall-clock timeout and (Windows only) a working-set
limit, and classifies the result: ok, insufficient-stack (controlled), out-of-memory (caught), exception,
stack-overflow (process killed by the runtime), timeout, memory-limit, or crash. Sizes are tried in ascending order and a
(scenario, operation) pair stops at its first non-ok outcome, so a failure is never retried at a larger size.

Compatible with Windows PowerShell 5.1. On Linux the probe is run through WSL from a self-contained linux-x64 publish
(see README.md); no memory limit is applied there.

.EXAMPLE
.\run-campaign.ps1 -ProbePath "$env:TEMP\probe-win\ExpressionResourceProbe.exe" -Scenarios A-left-sum -Operations rawhash -Output a.csv
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $ProbePath,
    [Parameter(Mandatory = $true)] [string[]] $Scenarios,
    [Parameter(Mandatory = $true)] [string[]] $Operations,
    [int[]] $Sizes = @(100, 250, 500, 1000, 2000, 3000, 5000, 10000, 20000),
    [string] $Thread = 'main',
    [int] $TimeoutSeconds = 60,
    [int] $MemoryLimitMB = 4096,
    [switch] $Counters,
    [switch] $Linux,
    [Parameter(Mandatory = $true)] [string] $Output
)

$ErrorActionPreference = 'Stop'

$linuxProbePath = $null
if ($Linux) {
    # wsl.exe strips backslashes from its arguments: hand wslpath a forward-slash Windows path.
    $linuxProbePath = (wsl -- wslpath -a ($ProbePath -replace '\\', '/')).Trim()
    if ($LASTEXITCODE -ne 0) { throw "wslpath failed with exit code $LASTEXITCODE." }
}

<#
.SYNOPSIS
Starts one probe process and returns its classified outcome and RESULT line.
#>
function Invoke-ProbeRun {
    param([string] $Scenario, [string] $Operation, [int] $Size)

    $probeArguments = @('run', $Scenario, $Size, $Operation, '--thread', $Thread)
    if ($Counters) { $probeArguments += '--counters' }

    $stdout = [System.IO.Path]::GetTempFileName()
    $stderr = [System.IO.Path]::GetTempFileName()
    try {
        if ($Linux) {
            $argumentList = @('--', 'timeout', '--signal=KILL', "$TimeoutSeconds", $linuxProbePath) + $probeArguments
            $process = Start-Process -FilePath 'wsl.exe' -ArgumentList $argumentList -NoNewWindow -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        }
        else {
            $process = Start-Process -FilePath $ProbePath -ArgumentList $probeArguments -NoNewWindow -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        }

        # Windows PowerShell 5.1 only reports ExitCode for a Start-Process object whose handle was read while running.
        $null = $process.Handle
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds + 5)
        $peakMB = 0
        $killedFor = $null
        while (-not $process.HasExited) {
            if ([DateTime]::UtcNow -gt $deadline) { $killedFor = 'timeout'; break }
            if (-not $Linux) {
                try {
                    $process.Refresh()
                    $currentMB = [int]($process.PeakWorkingSet64 / 1MB)
                    if ($currentMB -gt $peakMB) { $peakMB = $currentMB }
                    if ($currentMB -gt $MemoryLimitMB) { $killedFor = 'memory-limit'; break }
                }
                catch [System.InvalidOperationException] {
                    # The process exited between HasExited and Refresh.
                }
            }

            Start-Sleep -Milliseconds 100
        }

        if ($null -ne $killedFor) {
            try { $process.Kill() } catch [System.InvalidOperationException] { }
            $process.WaitForExit()
        }

        $process.WaitForExit()
        $exitCode = $process.ExitCode
        $resultLine = Get-Content -Path $stdout | Where-Object { $_ -like 'RESULT *' } | Select-Object -First 1
        $errorText = (Get-Content -Path $stderr -Raw)
        if ($null -eq $errorText) { $errorText = '' }

        $classification = if ($null -ne $killedFor) { $killedFor }
            elseif ($exitCode -eq 0) { 'ok' }
            elseif ($exitCode -eq 10) { 'insufficient-stack' }
            elseif ($exitCode -eq 11) { 'out-of-memory' }
            elseif ($exitCode -eq 12) { 'exception' }
            elseif ($exitCode -eq -1073741571 -or $errorText -match 'Stack overflow') { 'stack-overflow' }
            elseif ($Linux -and ($exitCode -eq 137 -or $exitCode -eq 9)) { 'timeout' } # SIGKILL from timeout(1), relayed by wsl.exe as 9
            else { 'crash' }

        [pscustomobject]@{
            scenario = $Scenario; operation = $Operation; size = $Size; thread = $Thread
            platform = $(if ($Linux) { 'linux' } else { 'windows' })
            classification = $classification; exitCode = $exitCode; peakWorkingSetMB = $peakMB
            result = $(if ($null -eq $resultLine) { '' } else { $resultLine })
        }
    }
    finally {
        Remove-Item -Path $stdout, $stderr -Force -ErrorAction SilentlyContinue
    }
}

$rows = New-Object System.Collections.Generic.List[object]
foreach ($scenario in $Scenarios) {
    foreach ($operation in $Operations) {
        foreach ($size in $Sizes) {
            $row = Invoke-ProbeRun -Scenario $scenario -Operation $operation -Size $size
            $rows.Add($row)
            Write-Host ("{0,-18} {1,-10} {2,7} {3,-18} {4}" -f $scenario, $operation, $size, $row.classification, ($row.result -replace '^RESULT .*? nodes=', 'nodes='))
            if ($row.classification -ne 'ok') { break }
        }
    }
}

$rows | Export-Csv -Path $Output -NoTypeInformation -Encoding UTF8
