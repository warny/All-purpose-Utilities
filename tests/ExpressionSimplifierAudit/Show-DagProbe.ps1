<#
.SYNOPSIS
    Prints the records of a dagprobe.jsonl file (see run-dagprobe.ps1) as one line per point.
#>
param([Parameter(Mandatory = $true)] [string] $Path)

$ErrorActionPreference = 'Stop'
Get-Content $Path | ForEach-Object {
    $r = $_ | ConvertFrom-Json
    $perOccurrence = ''
    if ($r.transformVisits -gt 0) { $perOccurrence = '{0:N2}/{1:N2}' -f ($r.transformVisits / $r.inputLogical), ($r.structuralVisits / $r.inputLogical) }
    '{0,-11} {1,3} in {2,5}/{3,12} | default {4,-28} {5,8:N1} ms | nodes off {14,-36} {15,9:N1} ms | unlimited {6,-20} {7,10:N1} ms {8,14:N0} B | out {9,6}/{10,14} | tv {11,10} sv {12,10} per occ {13}' -f `
        $r.family, $r.n, $r.inputDistinct, $r.inputLogical, $r.defaultOutcome, $r.defaultMs, $r.unlimitedOutcome, $r.unlimitedMs, $r.unlimitedBytes, $r.outputDistinct, $r.outputLogical, $r.transformVisits, $r.structuralVisits, $perOccurrence, $r.nodesOffOutcome, $r.nodesOffMs
}
