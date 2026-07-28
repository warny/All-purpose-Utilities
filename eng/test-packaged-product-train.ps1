[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()][string] $Configuration = "Release",
    [string] $ArtifactsPath = "artifacts",
    [switch] $SkipBuild,
    [switch] $SkipSourceLink,
    [switch] $KeepTemporaryProjects
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $ArtifactsPath))
$packagesPath = Join-Path $artifactsRoot "packages"
$temporaryPath = Join-Path $artifactsRoot "packaged-acceptance"
$globalPackages = Join-Path $temporaryPath "global-packages"
$configPath = Join-Path $temporaryPath "NuGet.config"
$originalNuGetPackages = $env:NUGET_PACKAGES
$env:DOTNET_ROLL_FORWARD = "Major"
$validationSucceeded = $false
$manifest = Get-Content (Join-Path $PSScriptRoot "parser-release-manifest.json") -Raw | ConvertFrom-Json
$versionProperties = [xml](Get-Content (Join-Path $repoRoot "Directory.Build.props") -Raw)
$expectedPackages = @{}
foreach ($package in $manifest.packages) {
    $expectedPackages[$package.packageId] = ([string]$versionProperties.Project.PropertyGroup.($package.versionProperty)).Trim()
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
    if (-not $SkipBuild) {
        & dotnet restore (Join-Path $repoRoot "Utils.sln")
        if ($LASTEXITCODE -ne 0) { throw "Solution restore failed." }
        & dotnet build (Join-Path $repoRoot "Utils.sln") --configuration $Configuration --no-restore
        if ($LASTEXITCODE -ne 0) { throw "Solution build failed." }
    }
    & (Join-Path $PSScriptRoot "pack-product-train.ps1") -Configuration $Configuration -ArtifactsPath $ArtifactsPath
    & (Join-Path $PSScriptRoot "inspect-packages.ps1") -ArtifactsPath $ArtifactsPath -SkipSourceLink:$SkipSourceLink

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

    $consumerRoot = Join-Path $repoRoot "tests/PackagedAcceptance"
    $projects = @(Get-ChildItem $consumerRoot -Filter *.csproj -Recurse -File)
    foreach ($project in $projects) {
        if ((Get-Content $project.FullName -Raw) -match "ProjectReference") { throw "$($project.FullName) contains a forbidden ProjectReference." }
        Write-Host "Restore: $($project.Name)"
        & dotnet restore $project.FullName --configfile $configPath --packages $globalPackages --no-cache --force
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
        & dotnet build $project.FullName --configuration $Configuration --no-restore -p:EnablePreviewFeatures=false
        if ($LASTEXITCODE -ne 0) { throw "Compile failed for $($project.FullName)." }
        & dotnet run --project $project.FullName --configuration $Configuration --no-build
        if ($LASTEXITCODE -ne 0) { throw "Execute failed for $($project.FullName)." }
        & dotnet list $project.FullName package --vulnerable --include-transitive --config $configPath
        if ($LASTEXITCODE -ne 0) { throw "Vulnerability audit failed for $($project.FullName)." }
    }

    $utilsProject = Join-Path $consumerRoot "UtilsConsumer/UtilsConsumer.csproj"
    & dotnet publish $utilsProject --configuration $Configuration --no-restore --output (Join-Path $temporaryPath "published-utils")
    if ($LASTEXITCODE -ne 0) { throw "Publish failed for the omy.Utils consumer." }
    & dotnet (Join-Path $temporaryPath "published-utils/UtilsConsumer.dll")
    if ($LASTEXITCODE -ne 0) { throw "Published omy.Utils consumer execution failed." }

    $generatorProject = Join-Path $consumerRoot "ParserGeneratorConsumer/ParserGeneratorConsumer.csproj"
    foreach ($emit in @("true", "false")) {
        foreach ($attach in @("true", "false")) {
            & dotnet build $generatorProject --configuration $Configuration --no-restore -p:EmitCompilerGeneratedFiles=$emit -p:UtilsParserAttachGeneratedFiles=$attach
            if ($LASTEXITCODE -ne 0) { throw "Generator matrix failed (Emit=$emit, Attach=$attach)." }
        }
    }

    $incrementalPath = Join-Path $temporaryPath "incremental-generator-consumer"
    New-Item (Join-Path $incrementalPath "Grammars") -ItemType Directory -Force | Out-Null
    Copy-Item (Join-Path $consumerRoot "ParserGeneratorConsumer/ParserGeneratorConsumer.csproj") $incrementalPath
    Copy-Item (Join-Path $consumerRoot "ParserGeneratorConsumer/Program.cs") $incrementalPath
    Copy-Item (Join-Path $consumerRoot "ParserGeneratorConsumer/Grammars/*.g4") (Join-Path $incrementalPath "Grammars")
    $incrementalProject = Join-Path $incrementalPath "ParserGeneratorConsumer.csproj"
    & dotnet restore $incrementalProject --configfile $configPath --packages $globalPackages --no-cache --force
    if ($LASTEXITCODE -ne 0) { throw "Incremental generator consumer restore failed." }

    # A real project build proves that imported changes replace, rather than accumulate with, generated output.
    & dotnet build $incrementalProject --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Initial incremental generator build failed." }
    & dotnet run --project $incrementalProject --configuration $Configuration --no-build -- "a" "importedLeaf"
    if ($LASTEXITCODE -ne 0) { throw "Initial incremental generator execution failed." }

    Set-Content (Join-Path $incrementalPath "Grammars/Shared.g4") "parser grammar Shared;`nchangedLeaf : TOKEN;"
    & dotnet build $incrementalProject --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Imported grammar modification rebuild failed." }
    & dotnet run --project $incrementalProject --configuration $Configuration --no-build -- "a" "changedLeaf" "importedLeaf"
    if ($LASTEXITCODE -ne 0) { throw "Imported grammar modification was not reflected in generated output." }

    Set-Content (Join-Path $incrementalPath "Grammars/Middle.g4") "parser grammar Middle;`nmiddleRule : TOKEN;"
    & dotnet build $incrementalProject --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Import removal rebuild failed." }
    & dotnet run --project $incrementalProject --configuration $Configuration --no-build -- "a" "local" "changedLeaf"
    if ($LASTEXITCODE -ne 0) { throw "Removed import left an effective generated rule." }

    $rootGrammar = Join-Path $incrementalPath "Grammars/Root.g4"
    (Get-Content $rootGrammar -Raw).Replace("tokenVocab=Tokens", "tokenVocab=TokensTwo") | Set-Content $rootGrammar
    & dotnet build $incrementalProject --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "tokenVocab change rebuild failed." }
    & dotnet run --project $incrementalProject --configuration $Configuration --no-build -- "z" "local" "changedLeaf" "false"
    if ($LASTEXITCODE -ne 0) { throw "tokenVocab change was not reflected in generated output." }

    Set-Content (Join-Path $incrementalPath "Grammars/Collision.g4") "parser grammar Collision;`nmiddleRule : TOKEN;`ncollisionOnly : TOKEN;"
    (Get-Content $rootGrammar -Raw).Replace("import Middle", "import Collision, Middle") | Set-Content $rootGrammar
    & dotnet build $incrementalProject --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Imported collision addition rebuild failed." }
    & dotnet run --project $incrementalProject --configuration $Configuration --no-build -- "z" "collisionOnly" "changedLeaf" "false"
    if ($LASTEXITCODE -ne 0) { throw "Imported collision was not composed deterministically." }
    Remove-Item (Join-Path $incrementalPath "Grammars/Collision.g4") -Force
    (Get-Content $rootGrammar -Raw).Replace("import Collision, Middle", "import Middle") | Set-Content $rootGrammar
    & dotnet build $incrementalProject --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Imported collision removal rebuild failed." }
    & dotnet run --project $incrementalProject --configuration $Configuration --no-build -- "z" "local" "collisionOnly" "false"
    if ($LASTEXITCODE -ne 0) { throw "Removed collision left stale generated output." }

    $diagnosticLog = Join-Path $temporaryPath "generator-diagnostics.log"
    Set-Content $rootGrammar "parser grammar Root;`nimport Missing;`nstart : 'a';"
    & dotnet build $incrementalProject --configuration $Configuration --no-restore 2>&1 | Tee-Object -FilePath $diagnosticLog
    if ($LASTEXITCODE -eq 0 -or -not (Select-String -Path $diagnosticLog -Pattern "UP0010" -Quiet)) { throw "Missing packaged import did not fail with UP0010." }

    Set-Content $rootGrammar "parser grammar Root;`nimport Middle;`nstart : 'a';"
    Set-Content (Join-Path $incrementalPath "Grammars/Middle.g4") "parser grammar Middle;`nimport Root;`nmiddleRule : 'a';"
    & dotnet build $incrementalProject --configuration $Configuration --no-restore 2>&1 | Tee-Object -FilePath $diagnosticLog
    if ($LASTEXITCODE -eq 0 -or -not (Select-String -Path $diagnosticLog -Pattern "UP0011" -Quiet)) { throw "Packaged import cycle did not fail with UP0011." }

    New-Item (Join-Path $incrementalPath "Grammars/one") -ItemType Directory -Force | Out-Null
    New-Item (Join-Path $incrementalPath "Grammars/two") -ItemType Directory -Force | Out-Null
    Set-Content $rootGrammar "parser grammar Root;`nimport Duplicate;`nstart : item;"
    Set-Content (Join-Path $incrementalPath "Grammars/one/Duplicate.g4") "parser grammar Duplicate;`nitem : 'a';"
    Set-Content (Join-Path $incrementalPath "Grammars/two/Duplicate.g4") "parser grammar Duplicate;`nitem : 'b';"
    & dotnet build $incrementalProject --configuration $Configuration --no-restore 2>&1 | Tee-Object -FilePath $diagnosticLog
    if ($LASTEXITCODE -eq 0 -or -not (Select-String -Path $diagnosticLog -Pattern "UP0016" -Quiet)) { throw "Ambiguous packaged import did not fail with UP0016." }

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
        & dotnet build-server shutdown --msbuild
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "dotnet build-server shutdown returned exit code $LASTEXITCODE; cleanup will still be attempted."
        }
        Remove-AcceptanceDirectory -Path $temporaryPath -IgnoreFailure
    }
}
