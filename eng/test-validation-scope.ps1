<# .SYNOPSIS Validates deterministic pull-request path classification. #>
[CmdletBinding()] param()
$script = Join-Path $PSScriptRoot 'get-validation-scope.ps1'
function Assert-Scope {
    param([string] $Path, [bool] $Train, [bool] $Scripts = $false)
    $scope = & $script -Paths $Path | ConvertFrom-Json
    if ($scope.runProductTrain -ne $Train -or $scope.runReleaseScriptTests -ne $Scripts) { throw "Unexpected scope for '$Path'." }
}
Assert-Scope 'README.md' $false
Assert-Scope 'src/Thing.cs' $true
Assert-Scope 'src/Thing.csproj' $true
Assert-Scope 'eng/tool.ps1' $true $true
Assert-Scope '.github/workflows/ci.yml' $true $true
Assert-Scope 'docs/releasing/guide.md' $true $true
Write-Host 'Validation-scope tests passed.'
