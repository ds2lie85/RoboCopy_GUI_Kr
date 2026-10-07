$ErrorActionPreference = "Stop"
Add-Type -AssemblyName PresentationFramework

$appName = "RoboCopy_GUI_Kr"
$installRoot = Join-Path $env:LOCALAPPDATA "Programs\$appName"
$shortcutPath = Join-Path ([Environment]::GetFolderPath("Programs")) "$appName.lnk"
$desktopShortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "$appName.lnk"
$uninstallRegistryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$appName"
$agentStartupRegistryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$agentStartupValueName = "RoboCopy_GUI_Kr_Agent"
$explorerMenuRegistryPaths = @(
    "HKCU:\Software\Classes\CLSID\{9A59C322-8A2D-4D57-B10D-39971ED3DF78}",
    "HKCU:\Software\Classes\AllFilesystemObjects\shell\RoboCopyGUIKr.Copy",
    "HKCU:\Software\Classes\Directory\shell\RoboCopyGUIKr.Copy",
    "HKCU:\Software\Classes\Directory\shell\RoboCopyGUIKr.Paste",
    "HKCU:\Software\Classes\Directory\Background\shell\RoboCopyGUIKr.Paste",
    "HKCU:\Software\Classes\Directory\shell\RoboCopyGUIKr.SetSource",
    "HKCU:\Software\Classes\Directory\shell\RoboCopyGUIKr.PasteSaved",
    "HKCU:\Software\Classes\Directory\Background\shell\RoboCopyGUIKr.PasteSaved",
    "HKCU:\Software\Classes\Directory\shell\RoboCopyGUIKr.PasteClipboard",
    "HKCU:\Software\Classes\Directory\Background\shell\RoboCopyGUIKr.PasteClipboard"
)

$confirmation = [System.Windows.MessageBox]::Show(
    "$appName 을 제거하시겠습니까? 실행 중인 로보카피 에이전트도 종료합니다. 작업 설정과 로그는 삭제하지 않습니다.",
    "$appName 제거",
    [System.Windows.MessageBoxButton]::YesNo,
    [System.Windows.MessageBoxImage]::Warning)
if ($confirmation -ne [System.Windows.MessageBoxResult]::Yes) {
    exit 0
}

Get-Process -Name "RoboCopy_GUI_Kr.Agent" -ErrorAction SilentlyContinue | Stop-Process -Force

$resolvedInstallRoot = [IO.Path]::GetFullPath($installRoot)
$expectedInstallRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA "Programs\$appName"))
if (-not [string]::Equals($resolvedInstallRoot, $expectedInstallRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "설치 경로 안전성 검증에 실패했습니다."
}

if (Test-Path -LiteralPath $shortcutPath) {
    Remove-Item -LiteralPath $shortcutPath -Force
}
if (Test-Path -LiteralPath $desktopShortcutPath) {
    Remove-Item -LiteralPath $desktopShortcutPath -Force
}
if (Test-Path -LiteralPath $uninstallRegistryPath) {
    Remove-Item -LiteralPath $uninstallRegistryPath -Recurse -Force
}
if (Test-Path -LiteralPath $agentStartupRegistryPath) {
    Remove-ItemProperty -LiteralPath $agentStartupRegistryPath -Name $agentStartupValueName -ErrorAction SilentlyContinue
}
foreach ($explorerMenuRegistryPath in $explorerMenuRegistryPaths) {
    if (Test-Path -LiteralPath $explorerMenuRegistryPath) {
        Remove-Item -LiteralPath $explorerMenuRegistryPath -Recurse -Force
    }
}
if (Test-Path -LiteralPath $resolvedInstallRoot) {
    Remove-Item -LiteralPath $resolvedInstallRoot -Recurse -Force
}

[System.Windows.MessageBox]::Show(
    "프로그램을 제거했습니다. 작업 설정과 로그는 유지되었습니다.",
    "$appName 제거",
    [System.Windows.MessageBoxButton]::OK,
    [System.Windows.MessageBoxImage]::Information) | Out-Null
