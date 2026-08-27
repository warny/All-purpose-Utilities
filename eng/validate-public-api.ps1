<#
.SYNOPSIS
Compares every explicitly declared public assembly with an exact diagnostic allowlist.
#>
[CmdletBinding()]
param(
    [string] $Configuration = "Release",
    [string] $ArtifactsPath = "artifacts",
    [TimeSpan] $ToolInstallTimeout = ([TimeSpan]::FromMinutes(5)),
    [TimeSpan] $ComparisonTimeout = ([TimeSpan]::FromMinutes(5))
)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Release.Common.ps1")

function Normalize-ApiMessage {
    param([string]$Message)
    return $Message.Trim().TrimEnd('.')
}
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-ProductTrainManifest $repoRoot
$artifactRoot = Resolve-RepositoryPath $repoRoot $ArtifactsPath
$packageRoot = Join-Path $artifactRoot 'packages'; $workRoot = Join-Path $artifactRoot 'api-compat'
Remove-Item $workRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item $workRoot -ItemType Directory -Force | Out-Null
$toolRoot = Join-Path $workRoot 'tool'
Invoke-NativeCommand -FilePath "dotnet" -ArgumentList @("tool", "install", "Microsoft.DotNet.ApiCompat.Tool", "--tool-path", $toolRoot, "--version", "10.0.302") -Timeout $ToolInstallTimeout -LogPath (Join-Path $workRoot "tool-install.log") | Out-Null
$tool = Join-Path $toolRoot $(if ($IsWindows) { 'apicompat.exe' } else { 'apicompat' })
$acceptanceCache = @{}
$results = @()

function Get-DeclaredAssembly {
    param([string] $ExpandedRoot, [string] $RelativePath, [string] $PackageId, [string] $Role)
    $path = Join-Path $ExpandedRoot ($RelativePath -replace '/', [IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "$PackageId`: declared $Role assembly '$RelativePath' is missing." }
    return Get-Item -LiteralPath $path
}

foreach ($package in $manifest.packages) {
    if (-not @($package.apiAssemblies).Count) { throw "$($package.packageId): apiAssemblies must be declared explicitly." }
    $candidateRoot = Join-Path $workRoot "candidate/$($package.packageId)"
    Expand-ZipArchive (Join-Path $packageRoot "$($package.packageId).$(Get-PackageVersion $manifest $package).nupkg") $candidateRoot
    $candidateAssemblies = @($package.apiAssemblies | ForEach-Object { Get-DeclaredAssembly $candidateRoot $_ $package.packageId 'candidate API' })
    $allowedAssemblyNames = @($candidateAssemblies.Name) + @($package.embeddedAssemblies)
    $unexpected = @(Get-ChildItem $candidateRoot -Filter *.dll -File -Recurse | Where-Object FullName -match '[\\/](lib|analyzers)[\\/]' | Where-Object Name -notin $allowedAssemblyNames)
    if ($unexpected) { throw "$($package.packageId): undeclared package assemblies: $($unexpected.FullName -join ', ')." }
    if (-not $package.publishedVersion) {
        $results += [ordered]@{ packageId=$package.packageId; baseline='first-candidate'; result='baseline-created'; breakingChanges=0; candidateAssemblies=@($package.apiAssemblies) }
        continue
    }
    if (@($package.baselineApiAssemblies).Count -ne $candidateAssemblies.Count) { throw "$($package.packageId): baselineApiAssemblies must map one-to-one to apiAssemblies." }
    $baselineVersion = [string]$package.publishedVersion
    $versions = @((Invoke-RestMethod "https://api.nuget.org/v3-flatcontainer/$($package.packageId.ToLowerInvariant())/index.json").versions)
    if ($versions -notcontains $baselineVersion) { throw "$($package.packageId): baseline '$baselineVersion' does not exist." }
    $latestStable = @($versions | Where-Object { $_ -notmatch '-' } | Select-Object -Last 1)[0]
    if ($latestStable -ne $baselineVersion) { throw "$($package.packageId): baseline '$baselineVersion' is not latest stable '$latestStable'." }
    $baselineFile = Join-Path $workRoot "$($package.packageId).$baselineVersion.nupkg"
    Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/$($package.packageId.ToLowerInvariant())/$baselineVersion/$($package.packageId.ToLowerInvariant()).$baselineVersion.nupkg" -OutFile $baselineFile
    $baselineRoot = Join-Path $workRoot "baseline/$($package.packageId)"; Expand-ZipArchive $baselineFile $baselineRoot
    $acceptancePath = Resolve-RepositoryPath $repoRoot ([string]$package.apiBreakAcceptanceFile)
    if (-not (Test-Path $acceptancePath)) { throw "$($package.packageId): API acceptance file '$($package.apiBreakAcceptanceFile)' does not exist." }
    if (-not $acceptanceCache.ContainsKey($acceptancePath)) {
        $acceptanceManifest = Get-Content $acceptancePath -Raw | ConvertFrom-Json
        $stableCandidateVersion = ([string]$manifest.version).Split('-', 2)[0]
        if ([string]$acceptanceManifest.version -ne $stableCandidateVersion) { throw "$($package.packageId): API acceptance manifest version '$($acceptanceManifest.version)' does not match candidate major release '$stableCandidateVersion'." }
        $acceptanceCache[$acceptancePath] = $acceptanceManifest
    }
    $acceptance = @($acceptanceCache[$acceptancePath].packages | Where-Object packageId -eq $package.packageId)
    if ($acceptance.Count -ne 1 -or $acceptance[0].baselineVersion -ne $baselineVersion) { throw "$($package.packageId): acceptance entry and baseline must match exactly once." }
    $actualKeys = @(); $additions = 0
    for ($index = 0; $index -lt $candidateAssemblies.Count; $index++) {
        $baselineDll = Get-DeclaredAssembly $baselineRoot $package.baselineApiAssemblies[$index] $package.packageId 'baseline API'
        $log = Join-Path $workRoot "$($package.packageId)-$index.txt"
        # ApiCompat intentionally returns a non-zero code when it emits compatibility
        # diagnostics. Capture that result instead of allowing the GitHub PowerShell
        # host's native-error preference to terminate the script before allowlist review.
        $comparison = Invoke-NativeCommand -FilePath $tool -ArgumentList @("-l", $baselineDll.FullName, "-r", $candidateAssemblies[$index].FullName) -Timeout $ComparisonTimeout -LogPath $log -IgnoreExitCode
        $exitCode = $comparison.ExitCode
        $output = @($comparison.StandardOutput -split "\r?\n") + @($comparison.StandardError -split "\r?\n")
        $assemblyDiagnostics = @($output | Where-Object { $_ -match '^CP\d+:' })
        if ($exitCode -ne 0 -and -not $assemblyDiagnostics) { throw "$($package.packageId): ApiCompat failed without producing compatibility diagnostics. See '$log'." }
        $actualKeys += @($assemblyDiagnostics | ForEach-Object { if ($_ -match '^(CP\d+):\s*(.*)$') { $message = Normalize-ApiMessage ($Matches[2].Replace($baselineDll.FullName, '{baselineAssembly}').Replace($candidateAssemblies[$index].FullName, '{candidateAssembly}')); "$($Matches[1])|$message" } })
        $reverseLog = Join-Path $workRoot "$($package.packageId)-$index-reverse.txt"
        $reverseResult = Invoke-NativeCommand -FilePath $tool -ArgumentList @("-l", $candidateAssemblies[$index].FullName, "-r", $baselineDll.FullName) -Timeout $ComparisonTimeout -LogPath $reverseLog -IgnoreExitCode
        $reverse = @($reverseResult.StandardOutput -split "\r?\n") + @($reverseResult.StandardError -split "\r?\n")
        if ($reverseResult.ExitCode -ne 0 -and -not @($reverse | Where-Object { $_ -match '^CP\d+:' })) { throw "$($package.packageId): reverse ApiCompat comparison failed. See '$reverseLog'." }
        $additions += @($reverse | Where-Object { $_ -match '^(CP0001|CP0002):' }).Count
    }

    $acceptedKeys = @($acceptance[0].acceptedDiagnostics | ForEach-Object {
        if ([string]::IsNullOrWhiteSpace($_.diagnosticId) -or [string]::IsNullOrWhiteSpace($_.message) -or [string]::IsNullOrWhiteSpace($_.reason) -or [string]::IsNullOrWhiteSpace($_.migrationSection)) { throw "$($package.packageId): every accepted diagnostic requires exact ID, message, reason, and migrationSection." }
        $migrationParts = ([string]$_.migrationSection).Split('#', 2)
        if ($migrationParts.Count -ne 2 -or [string]::IsNullOrWhiteSpace($migrationParts[1])) { throw "$($package.packageId): migrationSection '$($_.migrationSection)' must identify a checked-in document anchor." }
        $migrationPath = Resolve-RepositoryPath $repoRoot $migrationParts[0]
        if (-not (Test-Path -LiteralPath $migrationPath -PathType Leaf) -or -not (Select-String -LiteralPath $migrationPath -SimpleMatch "id=`"$($migrationParts[1])`"" -Quiet)) { throw "$($package.packageId): migration section '$($_.migrationSection)' does not exist." }
        "$($_.diagnosticId)|$(Normalize-ApiMessage $_.message)"
    })
    if (($acceptedKeys | Sort-Object -Unique).Count -ne $acceptedKeys.Count) { throw "$($package.packageId): duplicate API acceptance diagnostics." }
    $difference = if ($acceptedKeys.Count -eq 0 -and $actualKeys.Count -eq 0) { @() } elseif ($acceptedKeys.Count -eq 0) { @($actualKeys | ForEach-Object { [pscustomobject]@{ InputObject=$_; SideIndicator='=>' } }) } elseif ($actualKeys.Count -eq 0) { @($acceptedKeys | ForEach-Object { [pscustomobject]@{ InputObject=$_; SideIndicator='<=' } }) } else { @(Compare-Object ($acceptedKeys | Sort-Object) ($actualKeys | Sort-Object)) }
    if ($difference) { $formattedDifferences = $difference | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }; throw "$($package.packageId): API diagnostics differ from the exact allowlist (new or stale entries):`n$($formattedDifferences -join "`n")" }
    $breaking = $actualKeys.Count
    $results += [ordered]@{ packageId=$package.packageId; latestStable=$latestStable; latestPrerelease=($versions | Where-Object {$_ -match '-'} | Select-Object -Last 1); baseline=$baselineVersion; result=if($breaking){'accepted-major-version-breaks'}else{'compatible'}; breakingChanges=$breaking; compatibleAdditions=$additions; candidateAssemblies=@($package.apiAssemblies); baselineAssemblies=@($package.baselineApiAssemblies); acceptance=[string]$package.apiBreakAcceptanceFile }
}
$report = [ordered]@{ version=[string]$manifest.version; packages=$results }
Write-ReleaseJson $report (Join-Path $artifactRoot 'reports/public-api-comparison.json')
@('# Public API comparison','',"Candidate: ``$($manifest.version)``",'') + @($results | ForEach-Object { "- **$($_.packageId)**: baseline $($_.baseline); $($_.result); $($_.breakingChanges) exact accepted incompatibilities." }) | Set-Content (Join-Path $artifactRoot 'reports/public-api-comparison.md')
