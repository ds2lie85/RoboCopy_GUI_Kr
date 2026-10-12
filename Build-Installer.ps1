param(
    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64",
    [switch]$KeepStaging
)

$ErrorActionPreference = "Stop"
$repositoryRoot = $PSScriptRoot
$artifactsRoot = Join-Path $repositoryRoot "artifacts"
$installerRoot = Join-Path $artifactsRoot "installer"
$stagingRoot = Join-Path $installerRoot "staging"
$frameworkPublishRoot = Join-Path $stagingRoot "app-framework"
$offlinePublishRoot = Join-Path $stagingRoot "app-offline"
$packageRoot = Join-Path $stagingRoot "package"
$buildArtifactsRoot = Join-Path $stagingRoot "build"
$frameworkPayloadZip = Join-Path $packageRoot "payload-framework.zip"
$offlinePayloadZip = Join-Path $packageRoot "payload-offline.zip"
[xml]$versionProperties = Get-Content -LiteralPath (Join-Path $repositoryRoot 'Directory.Build.props') -Raw
$displayVersion = [string]$versionProperties.Project.PropertyGroup.InformationalVersion
$installerPath = Join-Path $installerRoot "RoboCopy_GUI_Kr_Setup_v${displayVersion}_x64.exe"
$projectPath = "src\RoboCopy_GUI_Kr\RoboCopy_GUI_Kr.csproj"
$workerProjectPath = "src\RoboCopy_GUI_Kr.ElevatedWorker\RoboCopy_GUI_Kr.ElevatedWorker.csproj"
$agentProjectPath = "src\RoboCopy_GUI_Kr.Agent\RoboCopy_GUI_Kr.Agent.csproj"

function Assert-PathUnderArtifacts([string]$path) {
    $resolvedArtifacts = [IO.Path]::GetFullPath($artifactsRoot).TrimEnd('\') + '\'
    $resolvedPath = [IO.Path]::GetFullPath($path)
    if (-not $resolvedPath.StartsWith($resolvedArtifacts, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside artifacts: $resolvedPath"
    }
}

Assert-PathUnderArtifacts $stagingRoot
if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}
Assert-PathUnderArtifacts $installerPath
if (Test-Path -LiteralPath $installerPath) {
    Remove-Item -LiteralPath $installerPath -Force
}
New-Item -ItemType Directory -Path $frameworkPublishRoot -Force | Out-Null
New-Item -ItemType Directory -Path $offlinePublishRoot -Force | Out-Null
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null

$cliHome = Join-Path $repositoryRoot ".dotnet_cli"
$cliRoamingData = Join-Path $cliHome "AppData\Roaming"
$cliLocalData = Join-Path $cliHome "AppData\Local"
New-Item -ItemType Directory -Path $cliRoamingData -Force | Out-Null
New-Item -ItemType Directory -Path $cliLocalData -Force | Out-Null
$env:DOTNET_CLI_HOME = $cliHome
$env:DOTNET_NOLOGO = "1"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:APPDATA = $cliRoamingData
$env:LOCALAPPDATA = $cliLocalData

$localDotnet = Join-Path $repositoryRoot ".dotnet\dotnet.exe"
$systemDotnet = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
$dotnetExecutable = if (Test-Path -LiteralPath $localDotnet) {
    $localDotnet
} elseif (Test-Path -LiteralPath $systemDotnet) {
    $systemDotnet
} else {
    (Get-Command dotnet -ErrorAction Stop).Source
}

Push-Location $repositoryRoot
try {
    & $dotnetExecutable restore $projectPath -r $Runtime --artifacts-path $buildArtifactsRoot --configfile "NuGet.Config" --disable-parallel -m:1 -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE." }

    & $dotnetExecutable publish $projectPath -c Release -r $Runtime --artifacts-path $buildArtifactsRoot -m:1 -p:UseSharedCompilation=false --no-restore --self-contained false `
        -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o $frameworkPublishRoot
    if ($LASTEXITCODE -ne 0) { throw "framework-dependent publish failed with exit code $LASTEXITCODE." }

    & $dotnetExecutable publish $projectPath -c Release -r $Runtime --artifacts-path $buildArtifactsRoot -m:1 -p:UseSharedCompilation=false --no-restore --self-contained true `
        -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o $offlinePublishRoot
    if ($LASTEXITCODE -ne 0) { throw "offline self-contained publish failed with exit code $LASTEXITCODE." }

    & $dotnetExecutable restore $workerProjectPath -r $Runtime --artifacts-path $buildArtifactsRoot --configfile "NuGet.Config" --disable-parallel -m:1 -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw "elevated worker restore failed with exit code $LASTEXITCODE." }
    & $dotnetExecutable publish $workerProjectPath -c Release -r $Runtime --artifacts-path $buildArtifactsRoot -m:1 -p:UseSharedCompilation=false --no-restore --self-contained false `
        -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o $frameworkPublishRoot
    if ($LASTEXITCODE -ne 0) { throw "framework elevated worker publish failed with exit code $LASTEXITCODE." }
    & $dotnetExecutable publish $workerProjectPath -c Release -r $Runtime --artifacts-path $buildArtifactsRoot -m:1 -p:UseSharedCompilation=false --no-restore --self-contained true `
        -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o $offlinePublishRoot
    if ($LASTEXITCODE -ne 0) { throw "offline elevated worker publish failed with exit code $LASTEXITCODE." }

    & $dotnetExecutable restore $agentProjectPath -r $Runtime --artifacts-path $buildArtifactsRoot --configfile "NuGet.Config" --disable-parallel -m:1 -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw "agent restore failed with exit code $LASTEXITCODE." }
    & $dotnetExecutable publish $agentProjectPath -c Release -r $Runtime --artifacts-path $buildArtifactsRoot -m:1 -p:UseSharedCompilation=false --no-restore --self-contained false `
        -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o $frameworkPublishRoot
    if ($LASTEXITCODE -ne 0) { throw "framework agent publish failed with exit code $LASTEXITCODE." }
    & $dotnetExecutable publish $agentProjectPath -c Release -r $Runtime --artifacts-path $buildArtifactsRoot -m:1 -p:UseSharedCompilation=false --no-restore --self-contained true `
        -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o $offlinePublishRoot
    if ($LASTEXITCODE -ne 0) { throw "offline agent publish failed with exit code $LASTEXITCODE." }
} finally {
    Pop-Location
}

$utf8WithBom = New-Object Text.UTF8Encoding($true)
& (Join-Path $repositoryRoot "installer\Copy-DistributionNotices.ps1") -PublishDirectory $offlinePublishRoot
foreach ($document in @("LICENSE", "README.md", "THIRD_PARTY_NOTICES.md", "third-party")) {
    Copy-Item -LiteralPath (Join-Path $offlinePublishRoot $document) -Destination $frameworkPublishRoot -Recurse -Force
}
$uninstallScript = Get-Content -LiteralPath (Join-Path $repositoryRoot "installer\Uninstall.ps1") -Raw -Encoding UTF8
foreach ($publishRoot in @($frameworkPublishRoot, $offlinePublishRoot)) {
    [IO.File]::WriteAllText((Join-Path $publishRoot "Uninstall.ps1"), $uninstallScript, $utf8WithBom)
}
Compress-Archive -Path (Join-Path $frameworkPublishRoot "*") -DestinationPath $frameworkPayloadZip -CompressionLevel Optimal -Force
Compress-Archive -Path (Join-Path $offlinePublishRoot "*") -DestinationPath $offlinePayloadZip -CompressionLevel Optimal -Force
$installScript = Get-Content -LiteralPath (Join-Path $repositoryRoot "installer\Install.ps1") -Raw -Encoding UTF8
[IO.File]::WriteAllText((Join-Path $packageRoot "Install.ps1"), $installScript, $utf8WithBom)
$installerStrings = Get-Content -LiteralPath (Join-Path $repositoryRoot 'installer\InstallerStrings.ko-KR.psd1') -Raw -Encoding UTF8
[IO.File]::WriteAllText((Join-Path $packageRoot 'InstallerStrings.ko-KR.psd1'), $installerStrings, $utf8WithBom)

$iexpressPath = Join-Path $env:WINDIR "System32\iexpress.exe"
if (-not (Test-Path -LiteralPath $iexpressPath)) {
    throw "Windows IExpress was not found: $iexpressPath"
}

$escapedPackageRoot = $packageRoot.TrimEnd('\') + '\'
$sedContent = @"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=
DisplayLicense=
FinishMessage=
TargetName=$installerPath
FriendlyName=RoboCopy_GUI_Kr $displayVersion Setup
AppLaunched=powershell.exe -NoProfile -ExecutionPolicy Bypass -File Install.ps1
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
SourceFiles=SourceFiles
[Strings]
FILE0=Install.ps1
FILE1=payload-framework.zip
FILE2=payload-offline.zip
FILE3=InstallerStrings.ko-KR.psd1
[SourceFiles]
SourceFiles0=$escapedPackageRoot
[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
%FILE3%=
"@
$sedPath = Join-Path $stagingRoot "installer.sed"
[IO.File]::WriteAllText($sedPath, $sedContent, [Text.Encoding]::Unicode)

& $iexpressPath /N /Q $sedPath
if ($LASTEXITCODE -ne 0) {
    throw "IExpress failed with exit code $LASTEXITCODE."
}
$installerDeadline = [DateTime]::UtcNow.AddSeconds(120)
$lastInstallerLength = -1
$stableLengthChecks = 0
while ([DateTime]::UtcNow -lt $installerDeadline) {
    if (Test-Path -LiteralPath $installerPath) {
        $currentInstallerLength = (Get-Item -LiteralPath $installerPath).Length
        if ($currentInstallerLength -gt 0 -and $currentInstallerLength -eq $lastInstallerLength) {
            $stableLengthChecks++
            if ($stableLengthChecks -ge 5) { break }
        } else {
            $stableLengthChecks = 0
            $lastInstallerLength = $currentInstallerLength
        }
    }
    Start-Sleep -Milliseconds 200
}
if (-not (Test-Path -LiteralPath $installerPath) -or $stableLengthChecks -lt 5) {
    throw "Installer was not created: $installerPath"
}

$frameworkBytes = (Get-ChildItem -LiteralPath $frameworkPublishRoot -File -Recurse | Measure-Object Length -Sum).Sum
$offlineBytes = (Get-ChildItem -LiteralPath $offlinePublishRoot -File -Recurse | Measure-Object Length -Sum).Sum
$installerBytes = (Get-Item -LiteralPath $installerPath).Length
Write-Host "Online/runtime-present payload size: $frameworkBytes bytes"
Write-Host "Offline self-contained payload size: $offlineBytes bytes"
Write-Host "Installer size: $installerBytes bytes"
Write-Host "Installer: $installerPath"
$installerHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $installerRoot 'SHA256SUMS.txt'),
    "$installerHash  $([IO.Path]::GetFileName($installerPath))`r`n", [Text.UTF8Encoding]::new($false))

Assert-PathUnderArtifacts $stagingRoot
if (-not $KeepStaging) { Remove-Item -LiteralPath $stagingRoot -Recurse -Force }
