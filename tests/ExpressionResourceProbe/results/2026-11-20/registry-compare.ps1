param(
    [Parameter(Mandatory = $true)][string[]]$Base,
    [Parameter(Mandatory = $true)][string[]]$Fix
)

$ErrorActionPreference = 'Stop'
$identity = @('scenario', 'size', 'diagnostics', 'peak', 'repeat', 'maxRetained')
$mustMatch = @('outcome', 'tokens', 'steps', 'nodes', 'retained', 'depth', 'visited', 'continuations', 'memoKeys', 'memoResults', 'contInvocations', 'lookahead', 'actions', 'tree', 'diag', 'trace')

function Read-Results([string]$path) {
    $map = [ordered]@{}
    foreach ($line in Get-Content $path) {
        $fields = @{}
        foreach ($part in ($line -split ' ')) {
            $index = $part.IndexOf('=')
            if ($index -gt 0) { $fields[$part.Substring(0, $index)] = $part.Substring($index + 1) }
        }
        $key = ($identity | ForEach-Object { $fields[$_] }) -join '|'
        $map[$key] = $fields
    }
    return $map
}

$baseRuns = @($Base | ForEach-Object { Read-Results $_ })
$fixRuns = @($Fix | ForEach-Object { Read-Results $_ })
$mismatches = 0
$rows = New-Object System.Collections.Generic.List[object]
foreach ($key in $baseRuns[0].Keys) {
    $all = @($baseRuns | ForEach-Object { $_[$key] }) + @($fixRuns | ForEach-Object { $_[$key] })
    foreach ($field in $mustMatch) {
        $values = @($all | ForEach-Object { $_[$field] } | Select-Object -Unique)
        if ($values.Count -ne 1) {
            $mismatches++
            Write-Output "MISMATCH $key $field : $($values -join ' / ')"
        }
    }

    $b = $baseRuns[0][$key]
    $f = $fixRuns[0][$key]
    function Mean([object[]]$runs, [string]$field) {
        $numbers = @($runs | ForEach-Object { $_[$key][$field] } | Where-Object { $_ -and $_ -ne '-' } | ForEach-Object { [double]$_ })
        if ($numbers.Count -eq 0) { return $null }
        return ($numbers | Measure-Object -Average).Average
    }
    $rows.Add([pscustomobject]@{
        Key = $key
        Retained = $b['retained']
        PeakBase = $b['peakRegistry']
        PeakFix = $f['peakRegistry']
        AllocBase = Mean $baseRuns 'allocated'
        AllocFix = Mean $fixRuns 'allocated'
        MsBase = Mean $baseRuns 'msMedian'
        MsFix = Mean $fixRuns 'msMedian'
        MsBaseRuns = (@($baseRuns | ForEach-Object { $_[$key]['msMedian'] }) -join '/')
        MsFixRuns = (@($fixRuns | ForEach-Object { $_[$key]['msMedian'] }) -join '/')
    })
}

Write-Output "comparisons=$($baseRuns[0].Keys.Count) mismatches=$mismatches"
$rows
