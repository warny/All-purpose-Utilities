<# .SYNOPSIS Validates deterministic pull-request path classification. #>
[CmdletBinding()] param()
$script = Join-Path $PSScriptRoot 'get-validation-scope.ps1'
function Assert-Scope {
    param([string[]] $Paths, [bool] $Train, [bool] $Scripts = $false, [bool] $DocumentationOnly = $false, [bool] $Vsix = $false)
    $scope = & $script -Paths $Paths | ConvertFrom-Json
    if ($scope.runProductTrain -ne $Train -or $scope.runReleaseScriptTests -ne $Scripts -or $scope.documentationOnly -ne $DocumentationOnly -or $scope.runVsix -ne $Vsix) {
        throw "Unexpected scope for '$($Paths -join ', ')'."
    }
}
Assert-Scope 'README.md' $false $false $true
Assert-Scope @('README.md', 'docs/guide.md') $false $false $true
Assert-Scope 'src/Thing.cs' $true
Assert-Scope 'src/Thing.csproj' $true
Assert-Scope 'eng/tool.ps1' $true $true
Assert-Scope '.github/workflows/ci.yml' $true $true
Assert-Scope 'docs/releasing/guide.md' $true $true
Assert-Scope @('README.md', 'src/unknown.build-input') $true
Assert-Scope 'Utils/README.md' $true
$manualScope = & $script -ForceProductTrain | ConvertFrom-Json
if (-not $manualScope.runProductTrain -or $manualScope.documentationOnly -or -not $manualScope.runVsix) {
    throw 'A forced manual validation did not run the complete product train.'
}
Assert-Scope 'Utils.Parser.VisualStudio/UtilsParserVisualStudioExtension.cs' $true $false $false $true
Assert-Scope 'Utils.Parser.VisualStudio.Worker/Program.cs' $true $false $false $true
Assert-Scope 'res/AllPurposeUtilities_logo.png' $true $false $false $true
Assert-Scope 'eng/test-vsix-package.ps1' $true $true $false $true
Assert-Scope 'Directory.Build.props' $true $false $false $true
Assert-Scope 'src/Thing.cs' $true $false $false $false
foreach ($path in @(
    'Utils.sln',
    'Utils.slnx',
    'src/Grammar.g4',
    'src/Resources/data.json',
    'src/Strings.resx',
    '.editorconfig',
    'config.ruleset',
    'NuGet.Config',
    'LICENSE-apache-2.0.txt',
    'src/unknown.build-input'
)) {
    Assert-Scope $path $true
}
Write-Host 'Validation-scope tests passed.'
