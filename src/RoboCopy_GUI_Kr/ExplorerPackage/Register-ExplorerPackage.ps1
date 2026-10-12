param([switch]$Unregister, [switch]$Installed)
$ErrorActionPreference = 'Stop'
$packageName = if ($Installed) { 'RoboCopyGUIKr.Explorer' } else { 'RoboCopyGUIKr.Development.Explorer' }
if ($Unregister) {
    # Only remove an identity that belongs to this application directory.
    Get-AppxPackage -Name $packageName | Where-Object {
        [string]::Equals($_.InstallLocation.TrimEnd('\'), $PSScriptRoot.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)
    } | Remove-AppxPackage
    return
}
if ([Environment]::OSVersion.Version.Build -lt 22000) { return }
$appRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath (Join-Path $appRoot 'RoboCopy_GUI_Kr.exe'))) {
    throw 'The development application must be published beside ExplorerPackage.'
}
# Register only the requested identity. Never enable Developer Mode, install
# certificates, restart Explorer, or shut down user processes automatically.
$manifest = Join-Path $PSScriptRoot 'AppxManifest.xml'
if ($Installed) {
    # Keep installed and development identities separate so their paths cannot collide.
    $manifest = Join-Path $PSScriptRoot 'AppxManifest.Installed.xml'
    [xml]$document = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'AppxManifest.xml') -Raw -Encoding UTF8
    $document.Package.Identity.Name = $packageName
    $document.Package.Identity.Publisher = 'CN=RoboCopyGUIKr'
    foreach ($attribute in $document.SelectNodes('//@Id | //@Clsid')) {
        if ($attribute.Value -eq 'D8EAC1AC-8469-42AB-B78A-DB861D0355A9') {
            $attribute.Value = '47F48C24-34BA-445C-8B98-578FA482374E'
        }
        elseif ($attribute.Value -eq 'CCB71B54-75C4-4BE7-AD86-E56A9B373F25') {
            $attribute.Value = 'F29604CF-E758-4D75-9653-C42949056A6F'
        }
    }
    $document.Save($manifest)
}
Add-AppxPackage -Register $manifest -ExternalLocation $appRoot
