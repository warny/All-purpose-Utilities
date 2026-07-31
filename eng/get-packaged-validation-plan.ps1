<#
.SYNOPSIS
Returns the deterministic packaged-consumer plan for a validation tier.
.DESCRIPTION
The plan is shared by packaged acceptance and offline tests so consumer coverage,
specialized matrices, and network audits cannot silently diverge.
#>
[CmdletBinding()]
param([ValidateSet('PullRequest', 'FullRelease')][string] $ValidationTier = 'FullRelease')
$consumerRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'tests/PackagedAcceptance'
$allConsumers = @(Get-ChildItem $consumerRoot -Filter '*.csproj' -Recurse -File | Sort-Object FullName)
$pullRequestNames = @(
    'UtilsConsumer.csproj',
    'ParserRuntimeConsumer.csproj',
    'ParserGeneratorConsumer.csproj',
    'DependencyInjectionGeneratorConsumer.csproj'
)
$selected = if ($ValidationTier -eq 'PullRequest') {
    @($allConsumers | Where-Object Name -in $pullRequestNames)
} else {
    $allConsumers
}
$consumers = @($selected | ForEach-Object {
    $xml = [xml](Get-Content $_.FullName -Raw)
    $frameworks = @($xml.Project.PropertyGroup.TargetFramework, $xml.Project.PropertyGroup.TargetFrameworks |
        Where-Object { $_ } | ForEach-Object { "$_" -split ';' } | Sort-Object -Unique)
    [ordered]@{
        name = $_.Name
        relativePath = [IO.Path]::GetRelativePath($consumerRoot, $_.FullName).Replace('\', '/')
        targetFrameworks = $frameworks
    }
})
[ordered]@{
    validationTier = $ValidationTier
    consumers = $consumers
    perConsumerVulnerabilityAudit = $ValidationTier -eq 'FullRelease'
    exhaustiveGeneratorMatrices = $ValidationTier -eq 'FullRelease'
    automaticConsumerPerPackage = $ValidationTier -eq 'FullRelease'
    remoteSourceLink = $ValidationTier -eq 'FullRelease'
    reproducibility = $ValidationTier -eq 'FullRelease'
    deprecatedAudit = $ValidationTier -eq 'FullRelease'
    outdatedAudit = $ValidationTier -eq 'FullRelease'
    candidateAssembly = $ValidationTier -eq 'FullRelease'
    publicationValidation = $ValidationTier -eq 'FullRelease'
} | ConvertTo-Json -Depth 6
