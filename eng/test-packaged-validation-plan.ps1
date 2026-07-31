<# .SYNOPSIS Validates offline packaged-consumer tier plans and framework coverage. #>
[CmdletBinding()] param()
$planner = Join-Path $PSScriptRoot 'get-packaged-validation-plan.ps1'
$pr = & $planner -ValidationTier PullRequest | ConvertFrom-Json
$full = & $planner -ValidationTier FullRelease | ConvertFrom-Json
$expectedPr = @('DependencyInjectionGeneratorConsumer.csproj', 'ParserGeneratorConsumer.csproj', 'ParserRuntimeConsumer.csproj', 'UtilsConsumer.csproj')
if (Compare-Object $expectedPr @($pr.consumers.name | Sort-Object)) { throw 'PullRequest consumers are not the documented deterministic subset.' }
$frameworks = @($pr.consumers.targetFrameworks | Sort-Object -Unique)
foreach ($required in @('net8.0', 'net9.0')) {
    if ($required -notin $frameworks) { throw "PullRequest consumers do not cover $required." }
}
if ($pr.perConsumerVulnerabilityAudit -or $pr.exhaustiveGeneratorMatrices -or $pr.automaticConsumerPerPackage -or $pr.remoteSourceLink -or $pr.reproducibility -or $pr.deprecatedAudit -or $pr.outdatedAudit -or $pr.candidateAssembly -or $pr.publicationValidation) {
    throw 'PullRequest plan schedules a full-release-only gate.'
}
$allProjects = @(Get-ChildItem (Join-Path (Split-Path -Parent $PSScriptRoot) 'tests/PackagedAcceptance') -Filter '*.csproj' -Recurse -File)
if ($full.consumers.Count -ne $allProjects.Count -or -not $full.perConsumerVulnerabilityAudit -or -not $full.exhaustiveGeneratorMatrices -or -not $full.automaticConsumerPerPackage -or -not $full.remoteSourceLink -or -not $full.reproducibility -or -not $full.deprecatedAudit -or -not $full.outdatedAudit -or -not $full.candidateAssembly -or -not $full.publicationValidation) {
    throw 'FullRelease plan is not exhaustive.'
}
Write-Host 'Packaged validation plan tests passed.'
