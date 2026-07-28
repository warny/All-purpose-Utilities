<#
.SYNOPSIS
Checks the candidate omy.Utils API against the published 1.2.1 binary baseline.
#>
[CmdletBinding()]
param([string] $Configuration = "Release", [string] $ArtifactsPath = "artifacts/api-compat")
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$output = [IO.Path]::GetFullPath((Join-Path $repoRoot $ArtifactsPath))
New-Item $output -ItemType Directory -Force | Out-Null
& dotnet build (Join-Path $repoRoot "Utils/Utils.csproj") -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "Candidate build failed." }
$package = Join-Path $output "omy.utils.1.2.1.nupkg"
Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/omy.utils/1.2.1/omy.utils.1.2.1.nupkg" -OutFile $package
Expand-Archive $package (Join-Path $output "baseline") -Force
& dotnet tool install Microsoft.DotNet.ApiCompat.Tool --tool-path (Join-Path $output "tool") --version 10.0.302
if ($LASTEXITCODE -ne 0) { throw "ApiCompat installation failed." }
$tool = Join-Path $output "tool/apicompat"
& $tool -l (Join-Path $output "baseline/lib/net8.0/Utils.dll") -r (Join-Path $repoRoot "Utils/bin/$Configuration/net8.0/Utils.dll") --suppression-file (Join-Path $PSScriptRoot "api-baselines/omy.Utils-1.2.1.xml")
if ($LASTEXITCODE -ne 0) { throw "The API delta differs from the reviewed 1.2.1 baseline." }
