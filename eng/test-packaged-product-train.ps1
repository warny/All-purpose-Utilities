[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()][string] $Configuration = "Release",
    [string] $ArtifactsPath = "artifacts",
    [ValidateSet("PullRequest", "FullRelease")][string] $ValidationTier = "FullRelease",
    [switch] $SkipBuild,
    [switch] $SkipSourceLink,
    [switch] $KeepTemporaryProjects,
    [switch] $TestNativeRunnerOnly,
    [switch] $UseExistingPackages,
    [switch] $ValidateExistingPackagesOnly
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "Release.Common.ps1")
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $ArtifactsPath))
$packagesPath = Join-Path $artifactsRoot "packages"
$temporaryPath = Join-Path $artifactsRoot "packaged-acceptance"
$globalPackages = Join-Path $temporaryPath "global-packages"
$configPath = Join-Path $temporaryPath "NuGet.config"
$originalNuGetPackages = $env:NUGET_PACKAGES
$env:DOTNET_ROLL_FORWARD = "Major"
$validationSucceeded = $false
$manifest = Get-Content (Join-Path $PSScriptRoot "product-train-manifest.json") -Raw | ConvertFrom-Json
$nativeLogRoot = Join-Path $artifactsRoot "logs/packaged-acceptance"
$dotnetPath = @(Get-Command dotnet -CommandType Application)[0].Source
$nativeCommandIndex = 0
if ($ValidateExistingPackagesOnly -and -not $UseExistingPackages) {
    throw "ValidateExistingPackagesOnly requires UseExistingPackages."
}

<# Runs every dotnet acceptance command with a command-specific timeout and log. #>
function Invoke-AcceptanceDotNet {
    param(
        [Parameter(Position = 0)][string] $Command,
        [Parameter(ValueFromRemainingArguments)][string[]] $RemainingArguments,
        [string[]] $Arguments
    )
    if ($null -eq $Arguments) { $Arguments = @($Command) + @($RemainingArguments) }
    $script:nativeCommandIndex++
    $operation = if ($Arguments.Count) { $Arguments[0] } else { "command" }
    $timeout = switch ($operation) {
        "restore" { [TimeSpan]::FromMinutes(10) }
        "build" { [TimeSpan]::FromMinutes(15) }
        "run" { [TimeSpan]::FromMinutes(5) }
        "publish" { [TimeSpan]::FromMinutes(10) }
        "list" { [TimeSpan]::FromMinutes(10) }
        "pack" { [TimeSpan]::FromMinutes(15) }
        default { [TimeSpan]::FromMinutes(5) }
    }
    $safeOperation = $operation -replace "[^A-Za-z0-9_.-]", "-"
    $logPath = Join-Path $nativeLogRoot ("{0:D3}-{1}.log" -f $script:nativeCommandIndex, $safeOperation)
    $result = Invoke-NativeCommand -FilePath $dotnetPath -ArgumentList $Arguments -Timeout $timeout -LogPath $logPath -IgnoreExitCode
    $global:LASTEXITCODE = $result.ExitCode
    if ($result.StandardOutput) { $result.StandardOutput -split "\r?\n" }
}

if ($TestNativeRunnerOnly) {
    & Invoke-AcceptanceDotNet --version | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Acceptance native-runner success probe failed." }
    & Invoke-AcceptanceDotNet -Arguments @("definitely-invalid-command", "-p:ReleaseRunnerProbe=Value") | Out-Null
    if ($LASTEXITCODE -eq 0) { throw "Acceptance native-runner failure probe unexpectedly succeeded." }
    if (@(Get-ChildItem $nativeLogRoot -Filter *.log -File).Count -ne 2) { throw "Acceptance native-runner did not create one log per command." }
    if (-not (Select-String -Path (Join-Path $nativeLogRoot "002-definitely-invalid-command.log") -SimpleMatch "-p:ReleaseRunnerProbe=Value" -Quiet)) { throw "Acceptance native runner did not preserve an MSBuild -p: argument." }
    Write-Host "Packaged acceptance native-runner tests passed."
    return
}

$expectedPackages = @{}
foreach ($package in $manifest.packages) {
    $expectedPackages[$package.packageId] = [string]$manifest.version
}

<# Verifies that the package directory still contains the exact canonical artifact set. #>
function Get-ValidatedCanonicalPackages {
    $reportPath = Join-Path $artifactsRoot "reports/canonical-packages.json"
    if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) { throw "Canonical package report is missing at '$reportPath'." }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    $commit = (& git -C $repoRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Unable to resolve the package-validation commit." }
    if ($report.productTrain -ne $manifest.productTrain -or $report.version -ne $manifest.version -or $report.commit -ne $commit) {
        throw "Canonical package report identity, version, or commit does not match this validation run."
    }
    $expectedFiles = @($manifest.packages | ForEach-Object {
        "$($_.packageId).$($manifest.version).nupkg"
        if ($_.symbolPackage) { "$($_.packageId).$($manifest.version).snupkg" }
    } | Sort-Object)
    $reportedFiles = @($report.packages.file | Sort-Object)
    $actualFiles = @(Get-ChildItem $packagesPath -File | Where-Object Extension -in @(".nupkg", ".snupkg") | Select-Object -ExpandProperty Name | Sort-Object)
    if (Compare-Object $expectedFiles $reportedFiles) { throw "canonical-packages.json does not declare the exact product-train package set." }
    if (Compare-Object $expectedFiles $actualFiles) { throw "Existing package directory does not contain the exact canonical package set." }
    foreach ($item in $report.packages) {
        $actualHash = (Get-FileHash (Join-Path $packagesPath $item.file) -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne $item.sha256) {
            throw "Canonical package '$($item.file)' no longer matches canonical-packages.json."
        }
    }
    return $report
}

$canonicalPackages = $null
if ($UseExistingPackages) {
    $canonicalPackages = Get-ValidatedCanonicalPackages
    if ($ValidateExistingPackagesOnly) {
        Write-Host "Existing canonical package validation passed; no package was built or packed."
        return
    }
}

<#
.SYNOPSIS
Removes an acceptance working directory with retries for Windows file-system delays.
.DESCRIPTION
Clears read-only attributes before every attempt. When cleanup follows successful
validation, callers can choose a warning instead of allowing a transient file lock to
change the acceptance result.
.PARAMETER Path
The directory to remove.
.PARAMETER IgnoreFailure
Writes a warning after all retries instead of throwing the final cleanup error.
#>
function Remove-AcceptanceDirectory {
    param(
        [Parameter(Mandatory)][string] $Path,
        [switch] $IgnoreFailure
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $lastError = $null
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            Get-ChildItem -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object {
                if ($_.Attributes -band [IO.FileAttributes]::ReadOnly) {
                    $_.Attributes = $_.Attributes -band (-bnot [IO.FileAttributes]::ReadOnly)
                }
            }
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
            return
        } catch {
            $lastError = $_
            if ($attempt -lt 5) {
                Start-Sleep -Milliseconds (250 * $attempt)
            }
        }
    }

    $message = "Unable to remove acceptance directory '$Path' after 5 attempts: $($lastError.Exception.Message)"
    if ($IgnoreFailure) {
        Write-Warning "$message The validated package artifacts were retained."
        return
    }

    throw $message
}

try {
    if (-not $UseExistingPackages -and -not $SkipBuild) {
        & Invoke-AcceptanceDotNet restore (Join-Path $repoRoot "Utils.sln")
        if ($LASTEXITCODE -ne 0) { throw "Solution restore failed." }
        & Invoke-AcceptanceDotNet build (Join-Path $repoRoot "Utils.sln") --configuration $Configuration --no-restore
        if ($LASTEXITCODE -ne 0) { throw "Solution build failed." }
    }
    if (-not $UseExistingPackages) {
        & (Join-Path $PSScriptRoot "pack-product-train.ps1") -Configuration $Configuration -ArtifactsPath $ArtifactsPath -NoBuild
        & (Join-Path $PSScriptRoot "inspect-packages.ps1") -ArtifactsPath $ArtifactsPath -SkipSourceLink:$SkipSourceLink
    }

    Remove-AcceptanceDirectory -Path $temporaryPath
    New-Item $globalPackages -ItemType Directory -Force | Out-Null
    # Do not isolate the solution build in this cache: MSBuild/SourceLink tasks loaded during
    # that phase can retain handles under NUGET_PACKAGES on Windows. Isolation starts only
    # after Build, Pack, and Inspect, immediately before packaged-consumer Restore.
    $env:NUGET_PACKAGES = $globalPackages
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources><clear /><add key="product-train" value="$packagesPath" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping><clear /><packageSource key="product-train"><package pattern="omy.*" /></packageSource><packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping>
</configuration>
"@ | Set-Content $configPath

    # Level A: every manifested package is restored and compiled in isolation. Library
    # packages are loaded by assembly name; analyzer packages must load in Roslyn without CS8032.
    $automaticRoot = Join-Path $temporaryPath "automatic-consumers"
    foreach ($package in $manifest.packages) {
        $safeName = $package.packageId.Replace('.', '-')
        $projectRoot = Join-Path $automaticRoot $safeName
        New-Item $projectRoot -ItemType Directory -Force | Out-Null
        $referenceMetadata = if ($package.kind -eq "analyzer") { ' OutputItemType="Analyzer" ReferenceOutputAssembly="false"' } else { '' }
        @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnablePreviewFeatures>false</EnablePreviewFeatures></PropertyGroup><ItemGroup><PackageReference Include="$($package.packageId)" Version="$($manifest.version)"$referenceMetadata /></ItemGroup></Project>
"@ | Set-Content (Join-Path $projectRoot "Consumer.csproj")
        if ($package.kind -eq "analyzer") {
            'Console.WriteLine("analyzer-loaded");' | Set-Content (Join-Path $projectRoot "Program.cs")
        } else {
            $assemblyName = [IO.Path]::GetFileNameWithoutExtension($package.project)
            "using System.Reflection; var assembly = Assembly.Load(`"$assemblyName`"); var representative = assembly.GetExportedTypes().FirstOrDefault() ?? throw new InvalidOperationException(`"No public type.`"); Console.WriteLine(`"assembly-loaded:${assemblyName}:`" + representative.FullName);" | Set-Content (Join-Path $projectRoot "Program.cs")
        }
        $automaticProject = Join-Path $projectRoot "Consumer.csproj"
        & Invoke-AcceptanceDotNet restore $automaticProject --configfile $configPath --packages $globalPackages
        if ($LASTEXITCODE -ne 0) { throw "Automatic restore failed for $($package.packageId)." }
        $automaticAssets = Get-Content (Join-Path $projectRoot "obj/project.assets.json") -Raw | ConvertFrom-Json
        foreach ($library in $automaticAssets.libraries.PSObject.Properties | Where-Object Name -like "omy.*/*") {
            $parts = $library.Name -split "/"
            if (-not $expectedPackages.ContainsKey($parts[0]) -or $parts[1] -ne $manifest.version) { throw "$($package.packageId) restored divergent internal asset '$($library.Name)'." }
        }
        $buildOutput = & Invoke-AcceptanceDotNet build $automaticProject --configuration $Configuration --no-restore 2>&1
        if ($LASTEXITCODE -ne 0 -or ($buildOutput -match "CS8032")) { $buildOutput | Write-Host; throw "Automatic compile/load gate failed for $($package.packageId)." }
        & Invoke-AcceptanceDotNet run --project $automaticProject --configuration $Configuration --no-build
        if ($LASTEXITCODE -ne 0) { throw "Automatic execution failed for $($package.packageId)." }
    }

    $consumerRoot = Join-Path $repoRoot "tests/PackagedAcceptance"
    $projects = @(Get-ChildItem $consumerRoot -Filter *.csproj -Recurse -File)
    # Pull requests retain representative root, composition, generator, net8 and net9 scenarios.
    $pullRequestConsumers = @(
        "UtilsConsumer.csproj",
        "ParserRuntimeConsumer.csproj",
        "ParserGeneratorConsumer.csproj",
        "DependencyInjectionGeneratorConsumer.csproj"
    )
    if ($ValidationTier -eq "PullRequest") {
        $projects = @($projects | Where-Object Name -in $pullRequestConsumers)
    }
    foreach ($project in $projects) {
        if ((Get-Content $project.FullName -Raw) -match "ProjectReference") { throw "$($project.FullName) contains a forbidden ProjectReference." }
        Write-Host "Restore: $($project.Name)"
        & Invoke-AcceptanceDotNet restore $project.FullName --configfile $configPath --packages $globalPackages
        if ($LASTEXITCODE -ne 0) { throw "Restore failed for $($project.FullName)." }
        $assetsPath = Join-Path $project.Directory.FullName "obj/project.assets.json"
        $assets = Get-Content $assetsPath -Raw | ConvertFrom-Json
        foreach ($library in $assets.libraries.PSObject.Properties | Where-Object Name -like "omy.*/*") {
            if ($library.Value.type -ne "package") { throw "$($library.Name) is not a package asset." }
            $parts = $library.Name -split "/"
            if (-not $expectedPackages.ContainsKey($parts[0])) { throw "$($library.Name) is not declared by the product-train manifest." }
            if ($parts[1] -ne $expectedPackages[$parts[0]]) { throw "$($library.Name) does not use the manifest version '$($expectedPackages[$parts[0]])'." }
            $packageFile = Join-Path $packagesPath "$($parts[0]).$($parts[1]).nupkg"
            if (-not (Test-Path $packageFile)) { throw "$($library.Name) does not match a local candidate package." }
        }
        & Invoke-AcceptanceDotNet -Arguments @("build", $project.FullName, "--configuration", $Configuration, "--no-restore", "-p:EnablePreviewFeatures=false")
        if ($LASTEXITCODE -ne 0) { throw "Compile failed for $($project.FullName)." }
        & Invoke-AcceptanceDotNet run --project $project.FullName --configuration $Configuration --no-build
        if ($LASTEXITCODE -ne 0) { throw "Execute failed for $($project.FullName)." }
        if ($ValidationTier -eq "FullRelease") {
            & Invoke-AcceptanceDotNet list $project.FullName package --vulnerable --include-transitive --config $configPath
            if ($LASTEXITCODE -ne 0) { throw "Vulnerability audit failed for $($project.FullName)." }
        }
    }

    # Every non-parser generator has a real consumer that executes generated behavior. Build
    # each with compiler-generated-file emission both enabled and disabled, then rebuild after
    # changing an input to exercise the incremental pipeline rather than merely loading Roslyn.
    $generatorConsumers = @(
        "ODataGeneratorConsumer/ODataGeneratorConsumer.csproj",
        "IOSerializationGeneratorConsumer/IOSerializationGeneratorConsumer.csproj",
        "DependencyInjectionGeneratorConsumer/DependencyInjectionGeneratorConsumer.csproj"
    )
    if ($ValidationTier -eq "PullRequest") {
        $generatorConsumers = @("DependencyInjectionGeneratorConsumer/DependencyInjectionGeneratorConsumer.csproj")
    }
    foreach ($relativeProject in $generatorConsumers) {
        $project = Join-Path $consumerRoot $relativeProject
        $projectDirectory = Split-Path -Parent $project
        $generatedDirectory = Join-Path $projectDirectory "obj/$Configuration/net9.0/generated"
        Remove-Item $generatedDirectory -Recurse -Force -ErrorAction SilentlyContinue
        foreach ($emit in @('true', 'false')) {
            & Invoke-AcceptanceDotNet -Arguments @("build", $project, "--configuration", $Configuration, "--no-restore", "-p:EmitCompilerGeneratedFiles=$emit")
            if ($LASTEXITCODE -ne 0) { throw "Generator consumer matrix failed for '$relativeProject' (Emit=$emit)." }
        }
        Remove-Item $generatedDirectory -Recurse -Force -ErrorAction SilentlyContinue
        & Invoke-AcceptanceDotNet -Arguments @("build", $project, "--configuration", $Configuration, "--no-restore", "--no-incremental", "-p:EmitCompilerGeneratedFiles=true")
        if ($LASTEXITCODE -ne 0) { throw "Generator baseline build failed for '$relativeProject'." }
        $program = Join-Path $projectDirectory 'Program.cs'
        $input = if ($relativeProject -like 'ODataGeneratorConsumer/*') { Join-Path $projectDirectory 'Sample.edmx' } else { $program }
        $originalInput = [IO.File]::ReadAllBytes($input)
        $originalText = [IO.File]::ReadAllText($input)
        $initialGeneratedHash = Get-GeneratedOutputFingerprint $generatedDirectory
        if ($relativeProject -like 'ODataGeneratorConsumer/*') {
            [IO.File]::WriteAllText($input, $originalText.Replace('Name="CategoryName"', 'Name="CategoryLabel"'))
        } elseif ($relativeProject -like 'IOSerializationGeneratorConsumer/*') {
            Add-Content $input "`n/// <summary>Provides an incremental generator input.</summary>`n[GenerateReaderWriter]`npublic partial class IncrementalPayload {`n/// <summary>Gets or sets the incremental value.</summary>`n[Field(0)] public int Value { get; set; } }"
        } else {
            Add-Content $input "`n/// <summary>Provides an incremental registration input.</summary>`n[Transient]`npublic sealed class IncrementalMessage : IMessage {`n/// <inheritdoc />`npublic string Value => `"incremental`"; }"
        }
        try {
            Remove-Item $generatedDirectory -Recurse -Force -ErrorAction SilentlyContinue
            & Invoke-AcceptanceDotNet -Arguments @("build", $project, "--configuration", $Configuration, "--no-restore", "--no-incremental", "-p:EmitCompilerGeneratedFiles=true")
            if ($LASTEXITCODE -ne 0) { throw "Incremental rebuild failed for '$relativeProject'." }
            $mutatedGeneratedHash = Get-GeneratedOutputFingerprint $generatedDirectory
            if ($mutatedGeneratedHash -eq $initialGeneratedHash) { throw "Generator output did not change after an input change for '$relativeProject'." }
        } finally {
            [IO.File]::WriteAllBytes($input, $originalInput)
        }
        Remove-Item $generatedDirectory -Recurse -Force -ErrorAction SilentlyContinue
        & Invoke-AcceptanceDotNet -Arguments @("build", $project, "--configuration", $Configuration, "--no-restore", "--no-incremental", "-p:EmitCompilerGeneratedFiles=true")
        if ($LASTEXITCODE -ne 0) { throw "Generator input restoration failed for '$relativeProject'." }
        $restoredGeneratedHash = Get-GeneratedOutputFingerprint $generatedDirectory
        if ($restoredGeneratedHash -ne $initialGeneratedHash) { throw "Restored generator output differs from the clean baseline for '$relativeProject'." }
        & Invoke-AcceptanceDotNet run --project $project --configuration $Configuration --no-build
        if ($LASTEXITCODE -ne 0) { throw "Generated behavior failed after incremental restoration for '$relativeProject'." }
    }

    $utilsProject = Join-Path $consumerRoot "UtilsConsumer/UtilsConsumer.csproj"
    & Invoke-AcceptanceDotNet publish $utilsProject --configuration $Configuration --no-restore --output (Join-Path $temporaryPath "published-utils")
    if ($LASTEXITCODE -ne 0) { throw "Publish failed for the omy.Utils consumer." }
    & Invoke-AcceptanceDotNet (Join-Path $temporaryPath "published-utils/UtilsConsumer.dll")
    if ($LASTEXITCODE -ne 0) { throw "Published omy.Utils consumer execution failed." }

    $generatorProject = Join-Path $consumerRoot "ParserGeneratorConsumer/ParserGeneratorConsumer.csproj"
    foreach ($emit in @("true", "false")) {
        foreach ($attach in @("true", "false")) {
            & Invoke-AcceptanceDotNet -Arguments @("build", $generatorProject, "--configuration", $Configuration, "--no-restore", "-p:EmitCompilerGeneratedFiles=$emit", "-p:UtilsParserAttachGeneratedFiles=$attach")
            if ($LASTEXITCODE -ne 0) { throw "Generator matrix failed (Emit=$emit, Attach=$attach)." }
        }
    }

    if ($ValidationTier -eq "FullRelease") {
        $incrementalPath = Join-Path $temporaryPath "incremental-generator-consumer"
    New-Item (Join-Path $incrementalPath "Grammars") -ItemType Directory -Force | Out-Null
    Copy-Item (Join-Path $consumerRoot "ParserGeneratorConsumer/ParserGeneratorConsumer.csproj") $incrementalPath
    Copy-Item (Join-Path $consumerRoot "ParserGeneratorConsumer/Program.cs") $incrementalPath
    Copy-Item (Join-Path $consumerRoot "ParserGeneratorConsumer/Grammars/*.g4") (Join-Path $incrementalPath "Grammars")
    $incrementalProject = Join-Path $incrementalPath "ParserGeneratorConsumer.csproj"
    & Invoke-AcceptanceDotNet restore $incrementalProject --configfile $configPath --packages $globalPackages
    if ($LASTEXITCODE -ne 0) { throw "Incremental generator consumer restore failed." }

    # A real project build proves that imported changes replace, rather than accumulate with, generated output.
    & Invoke-AcceptanceDotNet build $incrementalProject --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Initial incremental generator build failed." }
    & Invoke-AcceptanceDotNet run --project $incrementalProject --configuration $Configuration --no-build -- "a" "importedLeaf"
    if ($LASTEXITCODE -ne 0) { throw "Initial incremental generator execution failed." }

    Set-Content (Join-Path $incrementalPath "Grammars/Shared.g4") "parser grammar Shared;`nchangedLeaf : TOKEN;"
    & Invoke-AcceptanceDotNet build $incrementalProject --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Imported grammar modification rebuild failed." }
    & Invoke-AcceptanceDotNet run --project $incrementalProject --configuration $Configuration --no-build -- "a" "changedLeaf" "importedLeaf"
    if ($LASTEXITCODE -ne 0) { throw "Imported grammar modification was not reflected in generated output." }

    Set-Content (Join-Path $incrementalPath "Grammars/Middle.g4") "parser grammar Middle;`nmiddleRule : TOKEN;"
    & Invoke-AcceptanceDotNet build $incrementalProject --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Import removal rebuild failed." }
    & Invoke-AcceptanceDotNet run --project $incrementalProject --configuration $Configuration --no-build -- "a" "local" "changedLeaf"
    if ($LASTEXITCODE -ne 0) { throw "Removed import left an effective generated rule." }

    $rootGrammar = Join-Path $incrementalPath "Grammars/Root.g4"
    (Get-Content $rootGrammar -Raw).Replace("tokenVocab=Tokens", "tokenVocab=TokensTwo") | Set-Content $rootGrammar
    & Invoke-AcceptanceDotNet build $incrementalProject --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "tokenVocab change rebuild failed." }
    & Invoke-AcceptanceDotNet run --project $incrementalProject --configuration $Configuration --no-build -- "z" "local" "changedLeaf" "false"
    if ($LASTEXITCODE -ne 0) { throw "tokenVocab change was not reflected in generated output." }

    Set-Content (Join-Path $incrementalPath "Grammars/Collision.g4") "parser grammar Collision;`nmiddleRule : TOKEN;`ncollisionOnly : TOKEN;"
    (Get-Content $rootGrammar -Raw).Replace("import Middle", "import Collision, Middle") | Set-Content $rootGrammar
    & Invoke-AcceptanceDotNet build $incrementalProject --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Imported collision addition rebuild failed." }
    & Invoke-AcceptanceDotNet run --project $incrementalProject --configuration $Configuration --no-build -- "z" "collisionOnly" "changedLeaf" "false"
    if ($LASTEXITCODE -ne 0) { throw "Imported collision was not composed deterministically." }
    Remove-Item (Join-Path $incrementalPath "Grammars/Collision.g4") -Force
    (Get-Content $rootGrammar -Raw).Replace("import Collision, Middle", "import Middle") | Set-Content $rootGrammar
    & Invoke-AcceptanceDotNet build $incrementalProject --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Imported collision removal rebuild failed." }
    & Invoke-AcceptanceDotNet run --project $incrementalProject --configuration $Configuration --no-build -- "z" "local" "collisionOnly" "false"
    if ($LASTEXITCODE -ne 0) { throw "Removed collision left stale generated output." }

    $diagnosticLog = Join-Path $temporaryPath "generator-diagnostics.log"
    Set-Content $rootGrammar "parser grammar Root;`nimport Missing;`nstart : 'a';"
    & Invoke-AcceptanceDotNet build $incrementalProject --configuration $Configuration --no-restore 2>&1 | Tee-Object -FilePath $diagnosticLog
    if ($LASTEXITCODE -eq 0 -or -not (Select-String -Path $diagnosticLog -Pattern "UP0010" -Quiet)) { throw "Missing packaged import did not fail with UP0010." }

    Set-Content $rootGrammar "parser grammar Root;`nimport Middle;`nstart : 'a';"
    Set-Content (Join-Path $incrementalPath "Grammars/Middle.g4") "parser grammar Middle;`nimport Root;`nmiddleRule : 'a';"
    & Invoke-AcceptanceDotNet build $incrementalProject --configuration $Configuration --no-restore 2>&1 | Tee-Object -FilePath $diagnosticLog
    if ($LASTEXITCODE -eq 0 -or -not (Select-String -Path $diagnosticLog -Pattern "UP0011" -Quiet)) { throw "Packaged import cycle did not fail with UP0011." }

    New-Item (Join-Path $incrementalPath "Grammars/one") -ItemType Directory -Force | Out-Null
    New-Item (Join-Path $incrementalPath "Grammars/two") -ItemType Directory -Force | Out-Null
    Set-Content $rootGrammar "parser grammar Root;`nimport Duplicate;`nstart : item;"
    Set-Content (Join-Path $incrementalPath "Grammars/one/Duplicate.g4") "parser grammar Duplicate;`nitem : 'a';"
    Set-Content (Join-Path $incrementalPath "Grammars/two/Duplicate.g4") "parser grammar Duplicate;`nitem : 'b';"
    & Invoke-AcceptanceDotNet build $incrementalProject --configuration $Configuration --no-restore 2>&1 | Tee-Object -FilePath $diagnosticLog
    if ($LASTEXITCODE -eq 0 -or -not (Select-String -Path $diagnosticLog -Pattern "UP0016" -Quiet)) { throw "Ambiguous packaged import did not fail with UP0016." }

    }

    $specializedPackageIds = @($projects | ForEach-Object {
        $content = Get-Content $_.FullName -Raw
        [regex]::Matches($content, 'PackageReference Include="(omy\.[^"]+)"') | ForEach-Object { $_.Groups[1].Value }
    } | Sort-Object -Unique)
    $commit = (& git -C $repoRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Unable to resolve the packaged-acceptance commit." }
    $validatedArtifacts = if ($null -ne $canonicalPackages) { @($canonicalPackages.packages | ForEach-Object { [ordered]@{ file = $_.file; sha256 = $_.sha256 } }) } else { @() }
    $acceptanceReport = [ordered]@{ productTrain = [string]$manifest.productTrain; commit = $commit; version = [string]$manifest.version; platform = [Runtime.InteropServices.RuntimeInformation]::OSDescription; artifacts = $validatedArtifacts; packages = @($manifest.packages | ForEach-Object { [ordered]@{ packageId = $_.packageId; restored = $true; compiled = $true; executed = $true; audited = ($ValidationTier -eq "FullRelease"); assemblyLoaded = ($_.kind -ne 'analyzer'); publicTypeFound = ($_.kind -ne 'analyzer'); analyzerLoaded = ($_.kind -eq 'analyzer'); functionalScenarioExecuted = ($specializedPackageIds -contains $_.packageId); profile = $_.acceptanceProfile } }); specializedConsumers = @($projects.Name); passed = $true }
    $reportDirectory = Join-Path $artifactsRoot "reports"
    New-Item $reportDirectory -ItemType Directory -Force | Out-Null
    $acceptanceReport | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $reportDirectory "packaged-acceptance.json")
    $validationSucceeded = $true
    Write-Host "Validate: packaged product train passed. No package was published."
} finally {
    $env:NUGET_PACKAGES = $originalNuGetPackages
    if ($KeepTemporaryProjects) {
        Write-Host "Temporary acceptance directory retained at '$temporaryPath'."
    } elseif (-not $validationSucceeded) {
        Write-Warning "Packaged acceptance failed; temporary projects and logs were retained at '$temporaryPath'."
    } else {
        # Compiler and MSBuild servers can retain analyzer or SourceLink assemblies on Windows.
        # Shutting them down releases those handles before best-effort cleanup.
        & Invoke-AcceptanceDotNet build-server shutdown --msbuild
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "dotnet build-server shutdown returned exit code $LASTEXITCODE; cleanup will still be attempted."
        }
        Remove-AcceptanceDirectory -Path $temporaryPath -IgnoreFailure
    }
}
