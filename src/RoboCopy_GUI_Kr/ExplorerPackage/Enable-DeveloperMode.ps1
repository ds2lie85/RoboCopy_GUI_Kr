$ErrorActionPreference = 'Stop'
# Only this small worker requests elevation. It changes one machine setting;
# no certificates, SSH, device discovery, Device Portal, or app installation.
if ([Environment]::OSVersion.Version.Build -lt 22000) { exit 10 }
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal $identity
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { exit 11 }
$registry = $null
$developerKey = $null
try {
    $registry = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
        [Microsoft.Win32.RegistryHive]::LocalMachine, [Microsoft.Win32.RegistryView]::Registry64)
    $developerKey = $registry.CreateSubKey('SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock')
    $developerKey.SetValue('AllowDevelopmentWithoutDevLicense', 1, [Microsoft.Win32.RegistryValueKind]::DWord)
    if ($developerKey.GetValue('AllowDevelopmentWithoutDevLicense') -ne 1) { exit 12 }
} catch { Write-Error $_ -ErrorAction Continue; exit 12 }
finally {
    if ($null -ne $developerKey) { $developerKey.Dispose() }
    if ($null -ne $registry) { $registry.Dispose() }
}
exit 0
