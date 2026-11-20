param(
    [Parameter(Mandatory = $true)][string[]]$Base,
    [Parameter(Mandatory = $true)][string[]]$Branch
)

# Compares lookahead-campaign.ps1 outputs: every behavioral field must be identical across all files (base and branch),
# then prints the memory and timing summary (means over the files of each side).

$ErrorActionPreference = 'Stop'

$behavior = @('exit', 'tokens', 'outcome', 'steps', 'nodes', 'retained', 'depth', 'visited', 'visitedGroups', 'contInvocations',
    'continuations', 'memoKeys', 'memoResults', 'lookahead', 'actions', 'tree', 'diag', 'trace', 'lookaheadDigest', 'lstatsContent')

function Read-Campaign([string]$path) {
    $rows = @{}
    foreach ($line in Get-Content -Path $path -Encoding UTF8) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split ' '
        $fields = @{ exit = $parts[0] }
        foreach ($part in $parts | Select-Object -Skip 2) {
            $index = $part.IndexOf('=')
            if ($index -gt 0) { $fields[$part.Substring(0, $index)] = $part.Substring($index + 1) }
        }

        # The layout item of the characterization differs by construction; everything after it is content.
        if ($fields.ContainsKey('lstats')) { $fields['lstatsContent'] = ($fields['lstats'] -split ',' | Select-Object -Skip 1) -join ',' }
        $mode = if ($fields['lookaheadDigest'] -and $fields['lookaheadDigest'] -ne '-') { 'lookahead' } elseif ($fields['breakdown'] -and $fields['breakdown'] -ne '-') { 'breakdown' } elseif ($fields['peak'] -eq 'True') { 'peak' } else { 'timing' }
        $key = '{0} {1} diag={2} max={3} {4}' -f $fields['scenario'], $fields['size'], $fields['diagnostics'], $fields['maxRetained'], $mode
        $rows[$key] = $fields
    }

    return $rows
}

$baseRuns = @($Base | ForEach-Object { Read-Campaign $_ })
$branchRuns = @($Branch | ForEach-Object { Read-Campaign $_ })
$reference = $baseRuns[0]
$differences = 0
foreach ($key in ($reference.Keys | Sort-Object)) {
    foreach ($run in $baseRuns + $branchRuns) {
        if (-not $run.ContainsKey($key)) { Write-Output "MISSING $key"; $differences++; continue }
        foreach ($field in $behavior) {
            if ($reference[$key][$field] -ne $run[$key][$field]) {
                Write-Output ("DIFF {0} {1}: {2} vs {3}" -f $key, $field, $reference[$key][$field], $run[$key][$field])
                $differences++
            }
        }
    }
}

Write-Output ("configurations={0} runs={1} differences={2}" -f $reference.Count, ($baseRuns.Count + $branchRuns.Count), $differences)

function Mean([object[]]$runs, [string]$key, [scriptblock]$value) {
    $values = @($runs | ForEach-Object { & $value $_[$key] })
    return ($values | Measure-Object -Average).Average
}

function Lookahead-Bytes($fields) {
    $total = 0
    foreach ($item in $fields['breakdown'] -split ',') {
        $pair = $item -split ':'
        if ($pair[0] -like 'lookahead*') { $total += [long]$pair[1] }
    }
    return $total
}

function Registry-Bytes($fields) {
    $total = 0
    foreach ($item in $fields['breakdown'] -split ',') {
        $pair = $item -split ':'
        if ($pair[0] -notlike 'lookahead*' -and $pair.Count -eq 2) { $total += [long]$pair[1] }
    }
    return $total
}

Write-Output ''
Write-Output '| Scenario | Look-ahead entries | Cache base -> branch | Registry (other collections) | Parse peak (peakRegistry) base -> branch | livePeak base -> branch | liveAfter base -> branch |'
Write-Output '|---|---|---|---|---|---|---|'
foreach ($key in ($reference.Keys | Where-Object { $_ -like '* max=default breakdown' } | Sort-Object)) {
    $peakKey = $key -replace ' breakdown$', ' peak'
    $cacheBase = Mean $baseRuns $key { param($f) Lookahead-Bytes $f }
    $cacheBranch = Mean $branchRuns $key { param($f) Lookahead-Bytes $f }
    $registryBase = Mean $baseRuns $key { param($f) Registry-Bytes $f }
    $registryBranch = Mean $branchRuns $key { param($f) Registry-Bytes $f }
    $peakBase = Mean $baseRuns $peakKey { param($f) [long]$f['peakRegistry'] }
    $peakBranch = Mean $branchRuns $peakKey { param($f) [long]$f['peakRegistry'] }
    $liveBase = Mean $baseRuns $peakKey { param($f) [long]$f['livePeak'] }
    $liveBranch = Mean $branchRuns $peakKey { param($f) [long]$f['livePeak'] }
    $afterBase = Mean $baseRuns $peakKey { param($f) [long]$f['liveAfter'] }
    $afterBranch = Mean $branchRuns $peakKey { param($f) [long]$f['liveAfter'] }
    Write-Output ('| {0} | {1} | {2:N0} -> {3:N0} ({4:P0}) | {5:N0} -> {6:N0} | {7:N0} -> {8:N0} ({9:P0}) | {10:N0} -> {11:N0} ({12:P0}) | {13:N0} -> {14:N0} |' -f ($key -replace ' max=default breakdown$', ''), $reference[$key]['lookahead'],
        $cacheBase, $cacheBranch, (($cacheBranch - $cacheBase) / $cacheBase), $registryBase, $registryBranch,
        $peakBase, $peakBranch, (($peakBranch - $peakBase) / $peakBase), $liveBase, $liveBranch, (($liveBranch - $liveBase) / $liveBase), $afterBase, $afterBranch)
}

Write-Output ''
Write-Output '| Scenario | Allocated base -> branch | Median ms per file, base | Median ms per file, branch |'
Write-Output '|---|---|---|---|'
foreach ($key in ($reference.Keys | Where-Object { $_ -like '* timing' } | Sort-Object)) {
    $allocatedBase = Mean $baseRuns $key { param($f) [long]$f['allocated'] }
    $allocatedBranch = Mean $branchRuns $key { param($f) [long]$f['allocated'] }
    $timesBase = ($baseRuns | ForEach-Object { $_[$key]['msMedian'] }) -join ' / '
    $timesBranch = ($branchRuns | ForEach-Object { $_[$key]['msMedian'] }) -join ' / '
    Write-Output ('| {0} | {1:N0} -> {2:N0} ({3:P1}) | {4} | {5} |' -f ($key -replace ' max=default timing$', ''), $allocatedBase, $allocatedBranch, (($allocatedBranch - $allocatedBase) / $allocatedBase), $timesBase, $timesBranch)
}
