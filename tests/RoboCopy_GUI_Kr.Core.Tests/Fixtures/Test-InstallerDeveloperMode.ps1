param([string]$InstallerPath, [string]$ResourcePath, [string]$FixtureRoot, [string]$Scenario)
$ErrorActionPreference = 'Stop'
Import-LocalizedData -BindingVariable installerStrings -BaseDirectory (Split-Path -Parent $ResourcePath) -FileName (Split-Path -Leaf $ResourcePath)
$installRoot = $FixtureRoot
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($InstallerPath, [ref]$tokens, [ref]$errors)
if ($errors.Count -gt 0) { throw ($errors | Out-String) }
foreach ($name in @('Get-InstallerText', 'Set-Windows11ExplorerIntegration')) {
    $definition = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name }, $true)
    if ($null -eq $definition) { throw "Missing function: $name" }
    . ([scriptblock]::Create($definition.Extent.Text))
}
# Replace all machine-changing operations. These tests never request elevation,
# change HKLM, or register a real app package.
$script:enabled = $Scenario -eq 'AlreadyEnabled'
$script:elevations = 0
function Test-DeveloperModeEnabled { return $script:enabled }
function Enable-SelectedDeveloperMode {
    $script:elevations++
    if ($Scenario -eq 'Canceled') { throw 'UAC canceled' }
    $script:enabled = $true
}
$stubRoot = Join-Path $FixtureRoot 'ExplorerPackage'
New-Item -ItemType Directory -Path $stubRoot -Force | Out-Null
$stub = @'
param([switch]$Installed)
if (-not $Installed) { throw 'Installed identity was not selected' }
Set-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'registered.txt') -Value 'registered'
'@
Set-Content -LiteralPath (Join-Path $stubRoot 'Register-ExplorerPackage.ps1') -Value $stub
$registerMenu = $Scenario -ne 'MenuDisabled'
$selected = $Scenario -in @('Selected', 'Canceled', 'MenuDisabled', 'Windows10')
$build = if ($Scenario -eq 'Windows10') { 19045 } else { 26200 }
$failure = $false
try { $result = Set-Windows11ExplorerIntegration $registerMenu $selected $build }
catch { $failure = $true; $result = $_.Exception.Message }
[pscustomobject]@{
    Elevations = $script:elevations
    Enabled = $script:enabled
    Registered = Test-Path -LiteralPath (Join-Path $FixtureRoot 'registered.txt')
    Failed = $failure
    Result = $result
} | ConvertTo-Json -Compress
