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
$env:NUGET_PACKAGES = $globalPackages
$env:DOTNET_ROLL_FORWARD = "Major"
$validationSucceeded = $false

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
    $validationSucceeded = $true
    Write-Host "Validate: packaged product train passed. No package was published."
} finally {
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
