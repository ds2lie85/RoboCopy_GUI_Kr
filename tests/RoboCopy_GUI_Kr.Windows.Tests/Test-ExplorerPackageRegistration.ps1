param([string]$PackageSource, [string]$FixtureRoot, [switch]$Unregister)
$ErrorActionPreference = 'Stop'
$packageRoot = Join-Path $FixtureRoot 'ExplorerPackage'
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PackageSource 'AppxManifest.xml') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $PackageSource 'Register-ExplorerPackage.ps1') -Destination $packageRoot
Set-Content -LiteralPath (Join-Path $FixtureRoot 'RoboCopy_GUI_Kr.exe') -Value 'fixture'
# No real Appx operations. Exercise the production transformation and path guard.
function Add-AppxPackage {
    param([string]$Register, [string]$ExternalLocation)
    [pscustomobject]@{ Manifest = $Register; ExternalLocation = $ExternalLocation } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $FixtureRoot 'registration.json')
}
function Get-AppxPackage {
    param([string]$Name)
    [pscustomobject]@{ InstallLocation = $packageRoot; PackageFullName = 'owned' }
    [pscustomobject]@{ InstallLocation = ($packageRoot + '-other'); PackageFullName = 'unrelated' }
}
function Remove-AppxPackage {
    param([Parameter(ValueFromPipeline)]$Package)
    process { Add-Content -LiteralPath (Join-Path $FixtureRoot 'removed.txt') -Value $Package.PackageFullName }
}
if ($Unregister) { & (Join-Path $packageRoot 'Register-ExplorerPackage.ps1') -Installed -Unregister }
else { & (Join-Path $packageRoot 'Register-ExplorerPackage.ps1') -Installed }
