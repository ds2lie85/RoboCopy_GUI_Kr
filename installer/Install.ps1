$ErrorActionPreference = "Stop"
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Import-LocalizedData -BindingVariable installerStrings -BaseDirectory $PSScriptRoot -FileName 'InstallerStrings.ko-KR.psd1'

function Get-InstallerText([string]$key) { return $installerStrings[$key] }

$appName = "RoboCopy_GUI_Kr"
$displayVersion = "1.1"
$installRoot = Join-Path $env:LOCALAPPDATA "Programs\$appName"
$frameworkPayloadPath = Join-Path $PSScriptRoot "payload-framework.zip"
$offlinePayloadPath = Join-Path $PSScriptRoot "payload-offline.zip"
$executablePath = Join-Path $installRoot "$appName.exe"
$startMenuShortcutPath = Join-Path ([Environment]::GetFolderPath("Programs")) "$appName.lnk"
$desktopShortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "$appName.lnk"
$uninstallRegistryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$appName"
$runtimeDownloadUrl = "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe"
$minimumWindowsBuild = 14393
$minimumWindowsVersionName = "Windows 10 버전 1607"

function Test-DesktopRuntime8 {
    $registryPath = "HKLM:\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App"
    if (Test-Path -LiteralPath $registryPath) {
        $runtimeKey = Get-Item -LiteralPath $registryPath
        if ($runtimeKey.GetValueNames() | Where-Object { $_ -match '^8\.\d+\.\d+$' }) { return $true }
    }

    $dotnet = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if ($null -ne $dotnet) {
        $runtimeLines = & $dotnet.Source --list-runtimes 2>$null
        if ($runtimeLines | Where-Object { $_ -match '^Microsoft\.WindowsDesktop\.App 8\.' }) { return $true }
    }
    return $false
}

function Test-OfficialRuntimeOnline {
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        [Net.HttpWebRequest]$request = [Net.WebRequest]::Create($runtimeDownloadUrl)
        $request.Method = "GET"
        $request.AddRange(0, 0)
        $request.Timeout = 4000
        $request.AllowAutoRedirect = $true
        $response = $request.GetResponse()
        try { return $true } finally { $response.Dispose() }
    } catch {
        return $false
    }
}

function New-Shortcut([string]$shortcutPath) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $executablePath
    $shortcut.WorkingDirectory = $installRoot
    $shortcut.Description = $appName
    $shortcut.Save()
}

function Set-ExplorerIntegration([bool]$enabled) {
    $selectionHandlerClassId = "{9A59C322-8A2D-4D57-B10D-39971ED3DF78}"
    $classServerRoot = "HKCU:\Software\Classes\CLSID\$selectionHandlerClassId"
    $menuRoots = @(
        "HKCU:\Software\Classes\AllFilesystemObjects\shell\RoboCopyGUIKr.Copy",
        "HKCU:\Software\Classes\Directory\shell\RoboCopyGUIKr.Paste",
        "HKCU:\Software\Classes\Directory\Background\shell\RoboCopyGUIKr.Paste",
        "HKCU:\Software\Classes\Directory\shell\RoboCopyGUIKr.Copy"
    )
    foreach ($menuRoot in $menuRoots) {
        if (Test-Path -LiteralPath $menuRoot) { Remove-Item -LiteralPath $menuRoot -Recurse -Force }
    }
    if (Test-Path -LiteralPath $classServerRoot) { Remove-Item -LiteralPath $classServerRoot -Recurse -Force }
    if (-not $enabled) { return }

    $copyRoot = $menuRoots[0]
    $copyCommand = Join-Path $copyRoot "command"
    New-Item -Path $copyCommand -Force | Out-Null
    $copyCaption = "Robocopy$([char]0xB85C) $([char]0xBCF5)$([char]0xC0AC)"
    Set-Item -LiteralPath $copyRoot -Value $copyCaption
    New-ItemProperty -LiteralPath $copyRoot -Name Icon -Value $executablePath -PropertyType String -Force | Out-Null
    New-ItemProperty -LiteralPath $copyRoot -Name MultiSelectModel -Value "Player" -PropertyType String -Force | Out-Null
    Set-Item -LiteralPath $copyCommand -Value ""
    New-ItemProperty -LiteralPath $copyCommand -Name DelegateExecute -Value $selectionHandlerClassId -PropertyType String -Force | Out-Null

    $classServer = Join-Path $classServerRoot "LocalServer32"
    New-Item -Path $classServer -Force | Out-Null
    Set-Item -LiteralPath $classServer -Value "`"$executablePath`" --shell-com-server"
}

function Assert-SafeInstallPath {
    $resolvedInstallRoot = [IO.Path]::GetFullPath($installRoot)
    $expectedInstallRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA "Programs\$appName"))
    if (-not [string]::Equals($resolvedInstallRoot, $expectedInstallRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "설치 경로 안전성 검증에 실패했습니다."
    }
}

function Test-DeveloperModeEnabled {
    $registry = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
        [Microsoft.Win32.RegistryHive]::LocalMachine, [Microsoft.Win32.RegistryView]::Registry64)
    try {
        $key = $registry.OpenSubKey('SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock')
        if ($null -eq $key) { return $false }
        try { return $key.GetValue('AllowDevelopmentWithoutDevLicense') -eq 1 }
        finally { $key.Dispose() }
    } finally { $registry.Dispose() }
}

function Enable-SelectedDeveloperMode {
    if (Test-DeveloperModeEnabled) { return }
    $helper = Join-Path $installRoot 'ExplorerPackage\Enable-DeveloperMode.ps1'
    $powershell = Join-Path ([Environment]::GetFolderPath('System')) 'WindowsPowerShell\v1.0\powershell.exe'
    # Only the setting worker is elevated. Package registration stays with the
    # installing user, even if UAC is approved using a different administrator.
    $arguments = '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + $helper + '"'
    $process = Start-Process -FilePath $powershell -ArgumentList $arguments -Verb RunAs -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0 -or -not (Test-DeveloperModeEnabled)) {
        throw (Get-InstallerText 'DeveloperModeEnableFailed')
    }
}

function Set-Windows11ExplorerIntegration([bool]$registerExplorerMenu, [bool]$enableDeveloperMode, [int]$windowsBuild = [Environment]::OSVersion.Version.Build) {
    if (-not $registerExplorerMenu -or $windowsBuild -lt 22000) { return '' }
    if ($enableDeveloperMode) { Enable-SelectedDeveloperMode }
    if (-not (Test-DeveloperModeEnabled)) { return (Get-InstallerText 'ModernMenuLegacy') }
    $registrationScript = Join-Path $installRoot 'ExplorerPackage\Register-ExplorerPackage.ps1'
    & $registrationScript -Installed
    return (Get-InstallerText 'ModernMenuRegistered')
}

function Install-DesktopRuntime8 {
    $temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ("RoboCopy_GUI_Kr-" + [Guid]::NewGuid().ToString("N"))
    $runtimeInstaller = Join-Path $temporaryRoot "windowsdesktop-runtime-8-win-x64.exe"
    try {
        New-Item -ItemType Directory -Path $temporaryRoot -Force | Out-Null
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $webClient = New-Object Net.WebClient
        try { $webClient.DownloadFile($runtimeDownloadUrl, $runtimeInstaller) } finally { $webClient.Dispose() }
        $runtimeProcess = Start-Process -FilePath $runtimeInstaller -ArgumentList "/install", "/quiet", "/norestart" -Verb RunAs -Wait -PassThru
        if ($runtimeProcess.ExitCode -notin @(0, 1641, 3010)) {
            throw ".NET Desktop Runtime 설치 프로그램이 종료 코드 $($runtimeProcess.ExitCode)을 반환했습니다."
        }
    } finally {
        $expectedTemporaryParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
        $resolvedTemporaryRoot = [IO.Path]::GetFullPath($temporaryRoot)
        if ($resolvedTemporaryRoot.StartsWith($expectedTemporaryParent, [StringComparison]::OrdinalIgnoreCase) -and
            (Split-Path -Leaf $resolvedTemporaryRoot).StartsWith("RoboCopy_GUI_Kr-", [StringComparison]::Ordinal)) {
            Remove-Item -LiteralPath $resolvedTemporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function Install-Application([string]$payloadPath, [bool]$createStartMenuShortcut, [bool]$createDesktopShortcut, [bool]$registerExplorerMenu) {
    if (-not (Test-Path -LiteralPath $payloadPath)) { throw "선택한 설치 데이터 파일을 찾지 못했습니다." }
    Assert-SafeInstallPath
    if (Test-Path -LiteralPath $installRoot) {
        $replace = [System.Windows.MessageBox]::Show(
            "기존 설치를 새 버전으로 교체하시겠습니까? 사용자 작업 설정과 로그는 유지됩니다.", "$appName 설치",
            [System.Windows.MessageBoxButton]::YesNo, [System.Windows.MessageBoxImage]::Question)
        if ($replace -ne [System.Windows.MessageBoxResult]::Yes) { return $false }
        Remove-Item -LiteralPath $installRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
    Expand-Archive -LiteralPath $payloadPath -DestinationPath $installRoot -Force
    foreach ($shortcutPath in @($startMenuShortcutPath, $desktopShortcutPath)) {
        if (Test-Path -LiteralPath $shortcutPath) { Remove-Item -LiteralPath $shortcutPath -Force }
    }
    if ($createStartMenuShortcut) { New-Shortcut $startMenuShortcutPath }
    if ($createDesktopShortcut) { New-Shortcut $desktopShortcutPath }

    New-Item -Path $uninstallRegistryPath -Force | Out-Null
    New-ItemProperty -Path $uninstallRegistryPath -Name DisplayName -Value $appName -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallRegistryPath -Name DisplayVersion -Value $displayVersion -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallRegistryPath -Name Publisher -Value "RoboCopy_GUI_Kr" -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallRegistryPath -Name InstallLocation -Value $installRoot -PropertyType String -Force | Out-Null
    $estimatedSize = [int]((Get-ChildItem -LiteralPath $installRoot -File -Recurse | Measure-Object Length -Sum).Sum / 1KB)
    New-ItemProperty -Path $uninstallRegistryPath -Name EstimatedSize -Value $estimatedSize -PropertyType DWord -Force | Out-Null
    $uninstallCommand = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $installRoot 'Uninstall.ps1')`""
    New-ItemProperty -Path $uninstallRegistryPath -Name UninstallString -Value $uninstallCommand -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallRegistryPath -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $uninstallRegistryPath -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null
    Set-ExplorerIntegration $registerExplorerMenu
    return $true
}

$windowsSupported = [Environment]::Is64BitOperatingSystem -and [Environment]::OSVersion.Version.Build -ge $minimumWindowsBuild
$robocopyAvailable = $null -ne (Get-Command robocopy.exe -ErrorAction SilentlyContinue)
$runtimeInstalled = Test-DesktopRuntime8
$officialRuntimeOnline = Test-OfficialRuntimeOnline
$frameworkPayloadAvailable = Test-Path -LiteralPath $frameworkPayloadPath
$offlinePayloadAvailable = Test-Path -LiteralPath $offlinePayloadPath

[xml]$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Title="RoboCopy GUI 한국어 $displayVersion 설치"
        Width="760" Height="770" MinWidth="680" MinHeight="730" WindowStartupLocation="CenterScreen"
        ResizeMode="CanResize" Background="#F4F6F8">
  <Grid Margin="24">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/><RowDefinition Height="12"/><RowDefinition Height="Auto"/>
      <RowDefinition Height="8"/><RowDefinition Height="Auto"/><RowDefinition Height="16"/>
      <RowDefinition Height="Auto"/><RowDefinition Height="*"/><RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>
    <TextBlock Grid.Row="0" Text="RoboCopy GUI 한국어 $displayVersion 설치" FontSize="25" FontWeight="Bold" Foreground="#155A86"/>
    <TextBlock Grid.Row="2" Text="설치 환경을 자동 검사했습니다. 필요한 구성요소와 설치할 항목을 확인해 주세요."
               FontSize="14" TextWrapping="Wrap"/>
    <Border Grid.Row="4" Background="White" BorderBrush="#CDD6DD" BorderThickness="1" CornerRadius="6" Padding="16">
      <StackPanel>
        <TextBlock Text="필수 환경 검사" FontWeight="Bold" FontSize="15" Margin="0,0,0,10"/>
        <TextBlock Name="WindowsStatusText" Margin="0,3" TextWrapping="Wrap"/>
        <TextBlock Name="RobocopyStatusText" Margin="0,3" TextWrapping="Wrap"/>
        <TextBlock Name="InternetStatusText" Margin="0,3" TextWrapping="Wrap"/>
        <CheckBox Name="RuntimeCheckBox" Margin="0,8,0,2" FontWeight="SemiBold"
                  Content=".NET 8 Desktop Runtime (x64) 다운로드 및 설치"/>
        <TextBlock Name="RuntimeStatusText" Margin="24,0,0,2" Foreground="#4C5963" TextWrapping="Wrap"/>
      </StackPanel>
    </Border>
    <Border Grid.Row="6" Background="White" BorderBrush="#CDD6DD" BorderThickness="1" CornerRadius="6" Padding="16">
      <StackPanel>
        <TextBlock Text="설치 옵션" FontWeight="Bold" FontSize="15" Margin="0,0,0,10"/>
        <CheckBox Name="StartMenuCheckBox" Content="시작 메뉴 바로가기 만들기" IsChecked="True" Margin="0,3"/>
        <CheckBox Name="DesktopCheckBox" Content="바탕 화면 바로가기 만들기" IsChecked="False" Margin="0,3"/>
        <CheckBox Name="ExplorerCheckBox" Content="파일 탐색기 우클릭 메뉴 연동" IsChecked="True" Margin="0,3"/>
        <CheckBox Name="DeveloperModeCheckBox" IsChecked="False" Margin="0,6,0,3"/>
        <TextBlock Name="DeveloperModeWarningText" Margin="24,0,0,8" Foreground="#9A3412" TextWrapping="Wrap"/>
        <CheckBox Name="LaunchCheckBox" Content="설치 완료 후 프로그램 실행" IsChecked="True" Margin="0,3"/>
      </StackPanel>
    </Border>
    <TextBlock Name="InstallStatusText" Grid.Row="7" Margin="0,14,0,8" VerticalAlignment="Top"
               Foreground="#155A86" FontWeight="SemiBold" TextWrapping="Wrap"/>
    <StackPanel Grid.Row="8" Orientation="Horizontal" HorizontalAlignment="Right">
      <Button Name="CancelButton" Content="취소" Width="96" Height="34" Margin="0,0,8,0"/>
      <Button Name="InstallButton" Content="선택 항목 설치" Width="132" Height="34" Background="#176A9E" Foreground="White"/>
    </StackPanel>
  </Grid>
</Window>
"@

$reader = New-Object System.Xml.XmlNodeReader $xaml
$window = [Windows.Markup.XamlReader]::Load($reader)
$windowsStatusText = $window.FindName("WindowsStatusText")
$robocopyStatusText = $window.FindName("RobocopyStatusText")
$internetStatusText = $window.FindName("InternetStatusText")
$runtimeCheckBox = $window.FindName("RuntimeCheckBox")
$runtimeStatusText = $window.FindName("RuntimeStatusText")
$startMenuCheckBox = $window.FindName("StartMenuCheckBox")
$desktopCheckBox = $window.FindName("DesktopCheckBox")
$explorerCheckBox = $window.FindName("ExplorerCheckBox")
$developerModeCheckBox = $window.FindName('DeveloperModeCheckBox')
$developerModeWarningText = $window.FindName('DeveloperModeWarningText')
$developerModeCheckBox.Content = Get-InstallerText 'DeveloperModeOption'
$developerModeWarningText.Text = Get-InstallerText 'DeveloperModeWarning'
$windows11 = [Environment]::OSVersion.Version.Build -ge 22000
$developerModeCheckBox.Visibility = if ($windows11) { 'Visible' } else { 'Collapsed' }
$developerModeWarningText.Visibility = $developerModeCheckBox.Visibility
$developerModeCheckBox.IsEnabled = $windows11 -and $explorerCheckBox.IsChecked -eq $true
$explorerCheckBox.Add_Unchecked({ $developerModeCheckBox.IsChecked = $false; $developerModeCheckBox.IsEnabled = $false })
$explorerCheckBox.Add_Checked({ $developerModeCheckBox.IsEnabled = $windows11 })
$launchCheckBox = $window.FindName("LaunchCheckBox")
$installStatusText = $window.FindName("InstallStatusText")
$installButton = $window.FindName("InstallButton")
$cancelButton = $window.FindName("CancelButton")

$windowsStatusText.Text = if ($windowsSupported) { "✓ Windows x64 빌드 $([Environment]::OSVersion.Version.Build) — 지원됨" } else { "✗ $minimumWindowsVersionName(빌드 $minimumWindowsBuild) 이상, x64 운영체제가 필요합니다." }
$windowsStatusText.Foreground = if ($windowsSupported) { "#18743A" } else { "#B42318" }
$robocopyStatusText.Text = if ($robocopyAvailable) { "✓ Windows Robocopy — 사용 가능" } else { "✗ Windows Robocopy를 찾지 못했습니다." }
$robocopyStatusText.Foreground = if ($robocopyAvailable) { "#18743A" } else { "#B42318" }
$internetStatusText.Text = if ($officialRuntimeOnline) { "✓ Microsoft 공식 .NET 다운로드 연결 가능" } else { "• 인터넷 연결 없음 — 패키지에 포함된 오프라인 실행 파일을 사용합니다." }
$internetStatusText.Foreground = if ($officialRuntimeOnline) { "#18743A" } else { "#9A6700" }
if ($runtimeInstalled) {
    $runtimeCheckBox.IsChecked = $true
    $runtimeCheckBox.IsEnabled = $false
    $runtimeStatusText.Text = "설치됨 — 추가 다운로드가 필요하지 않습니다."
} elseif ($officialRuntimeOnline) {
    $runtimeCheckBox.IsChecked = $true
    $runtimeStatusText.Text = "선택 시 Microsoft 공식 런타임을 설치합니다. 선택을 해제하면 패키지 내 실행 파일을 사용합니다."
} else {
    $runtimeCheckBox.IsChecked = $false
    $runtimeCheckBox.IsEnabled = $false
    $runtimeStatusText.Text = "오프라인 상태이므로 런타임 DLL이 포함된 실행 파일을 설치합니다."
}
$installButton.IsEnabled = $windowsSupported -and $robocopyAvailable -and $frameworkPayloadAvailable -and ($runtimeInstalled -or $officialRuntimeOnline -or $offlinePayloadAvailable)
if (-not $installButton.IsEnabled) { $installStatusText.Text = "필수 환경을 충족하지 않아 설치를 계속할 수 없습니다." }

$cancelButton.Add_Click({ $window.DialogResult = $false; $window.Close() })
$installButton.Add_Click({
    try {
        $enableDeveloperMode = $developerModeCheckBox.IsChecked -eq $true -and $explorerCheckBox.IsChecked -eq $true -and $windows11
        if ($enableDeveloperMode) {
            $confirmation = [System.Windows.MessageBox]::Show($window,
                (Get-InstallerText 'DeveloperModeConfirm'), (Get-InstallerText 'DeveloperModeConfirmTitle'),
                [System.Windows.MessageBoxButton]::YesNo, [System.Windows.MessageBoxImage]::Warning)
            if ($confirmation -ne [System.Windows.MessageBoxResult]::Yes) { return }
        }
        $installButton.IsEnabled = $false
        $cancelButton.IsEnabled = $false
        $selectedPayloadPath = $frameworkPayloadPath
        if (-not $runtimeInstalled) {
            $installedOfficialRuntime = $false
            if ($officialRuntimeOnline -and $runtimeCheckBox.IsChecked -eq $true) {
                try {
                    $installStatusText.Text = ".NET 8 Desktop Runtime을 Microsoft 공식 서버에서 다운로드하고 설치하는 중입니다. 관리자 승인 창이 나타날 수 있습니다."
                    $window.Dispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::Background)
                    Install-DesktopRuntime8
                    $installedOfficialRuntime = Test-DesktopRuntime8
                } catch {
                    $installStatusText.Text = "공식 런타임 설치를 완료하지 못해 패키지 내 오프라인 실행 파일로 전환합니다."
                    $window.Dispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::Background)
                }
            }

            if (-not $installedOfficialRuntime) {
                if (-not $offlinePayloadAvailable) { throw "오프라인 실행 파일이 설치 패키지에 없습니다." }
                $selectedPayloadPath = $offlinePayloadPath
            }
        }

        $installStatusText.Text = "프로그램 파일을 설치하는 중입니다."
        $window.Dispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::Background)
        $installed = Install-Application $selectedPayloadPath ($startMenuCheckBox.IsChecked -eq $true) ($desktopCheckBox.IsChecked -eq $true) ($explorerCheckBox.IsChecked -eq $true)
        if (-not $installed) {
            $installButton.IsEnabled = $true; $cancelButton.IsEnabled = $true; $installStatusText.Text = "설치가 취소되었습니다."
            return
        }

        $menuStatus = ''
        try {
            $menuStatus = Set-Windows11ExplorerIntegration ($explorerCheckBox.IsChecked -eq $true) $enableDeveloperMode
        } catch {
            $menuStatus = (Get-InstallerText 'ModernMenuFailed') + "`n" + $_.Exception.Message
        }
        $completionText = (Get-InstallerText 'InstallCompleted') + "`n`n" + $menuStatus
        [System.Windows.MessageBox]::Show($completionText, "$appName 설치",
            [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Information) | Out-Null
        if ($launchCheckBox.IsChecked -eq $true) { Start-Process -FilePath $executablePath }
        $window.DialogResult = $true
        $window.Close()
    } catch {
        $installStatusText.Text = "설치하지 못했습니다: $($_.Exception.Message)"
        $installButton.IsEnabled = $windowsSupported -and $robocopyAvailable -and $frameworkPayloadAvailable -and ($runtimeInstalled -or $officialRuntimeOnline -or $offlinePayloadAvailable)
        $cancelButton.IsEnabled = $true
        [System.Windows.MessageBox]::Show("설치하지 못했습니다.`n`n$($_.Exception.Message)", "$appName 설치 오류",
            [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Error) | Out-Null
    }
})

$window.ShowDialog() | Out-Null
