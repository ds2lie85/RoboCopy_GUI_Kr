$ErrorActionPreference = "Stop"
$repositoryRoot = $PSScriptRoot
$cliHome = Join-Path $repositoryRoot ".dotnet_cli"
New-Item -ItemType Directory -Path $cliHome -Force | Out-Null
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

$developmentRoot = Join-Path $repositoryRoot "artifacts\development\RoboCopy_GUI_Kr_Development"
$developmentAppRoot = Join-Path $developmentRoot "app"
$developmentTestRoot = Join-Path $developmentRoot "tests"
$developmentToolRoot = Join-Path $developmentRoot "test-tools"
$developmentSourceRoot = Join-Path $developmentRoot "source"

function Register-DevelopmentExplorerMenu([string]$executablePath) {
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

    $copyRoot = $menuRoots[0]
    $copyCommand = Join-Path $copyRoot "command"
    New-Item -Path $copyCommand -Force | Out-Null
    $copyCaption = "Robocopy$([char]0xB85C) $([char]0xBCF5)$([char]0xC0AC)"
    Set-Item -LiteralPath $copyRoot -Value $copyCaption
    New-ItemProperty -LiteralPath $copyRoot -Name Icon -Value $executablePath -PropertyType String -Force | Out-Null
    New-ItemProperty -LiteralPath $copyRoot -Name MultiSelectModel -Value "Player" -PropertyType String -Force | Out-Null
    New-ItemProperty -LiteralPath $copyRoot -Name Position -Value "Top" -PropertyType String -Force | Out-Null
    Set-Item -LiteralPath $copyCommand -Value ""
    New-ItemProperty -LiteralPath $copyCommand -Name DelegateExecute -Value $selectionHandlerClassId -PropertyType String -Force | Out-Null

    $classServer = Join-Path $classServerRoot "LocalServer32"
    New-Item -Path $classServer -Force | Out-Null
    Set-Item -LiteralPath $classServer -Value "`"$executablePath`" --shell-com-server"
}

function Assert-DevelopmentPath([string]$path) {
    $expectedParent = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts\development")).TrimEnd('\') + '\'
    $resolvedPath = [IO.Path]::GetFullPath($path)
    if (-not $resolvedPath.StartsWith($expectedParent, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside artifacts\development: $resolvedPath"
    }
}

function Copy-SourceSnapshot([string]$destinationRoot) {
    $rootFiles = @(
        ".gitignore",
        ".gitattributes",
        ".gitlab-ci.yml",
        "AGENTS.md",
        "Build.ps1",
        "Build-Installer.ps1",
        "Directory.Build.props",
        "DEVELOPMENT_STATUS.md",
        "DEVELOPMENT_TOOLS.md",
        "global.json",
        "NuGet.Config",
        "README.md",
        "LICENSE",
        "THIRD_PARTY_NOTICES.md",
        "RELEASING.md",
        "Robocopy_GUI_개발계획서.md",
        "RoboCopy_GUI_Kr.sln",
        "Run-Development.ps1",
        "Setup-LocalSdk.ps1"
    )

    foreach ($relativePath in $rootFiles) {
        $sourcePath = Join-Path $repositoryRoot $relativePath
        if (Test-Path -LiteralPath $sourcePath) {
            $targetPath = Join-Path $destinationRoot $relativePath
            New-Item -ItemType Directory -Path (Split-Path -Parent $targetPath) -Force | Out-Null
            Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
        }
    }

    foreach ($treeName in @("src", "tests", "installer", "test-assets", ".github", "docs")) {
        $treeRoot = Join-Path $repositoryRoot $treeName
        Get-ChildItem -LiteralPath $treeRoot -File -Recurse -ErrorAction SilentlyContinue |
            Where-Object {
                ($_.FullName -notmatch '[\\/](bin|obj)[\\/]') -and
                ($_.FullName -notmatch '[\\/]test-assets[\\/].*[\\/]destination([\\/]|$)') -and
                ($_.Name -notin @(".lock-process-id", ".lock-ready", ".release-lock")) -and
                ($_.FullName -notmatch '[\\/]test-assets[\\/]elevated-retry[\\/](source|destination)([\\/]|$)')
            } |
            ForEach-Object {
                if (-not $_.FullName.StartsWith($repositoryRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
                    throw "Source file is outside the repository: $($_.FullName)"
                }
                $relativePath = $_.FullName.Substring($repositoryRoot.TrimEnd('\').Length).TrimStart('\')
                $targetPath = Join-Path $destinationRoot $relativePath
                New-Item -ItemType Directory -Path (Split-Path -Parent $targetPath) -Force | Out-Null
                Copy-Item -LiteralPath $_.FullName -Destination $targetPath -Force
            }
    }
}

Push-Location $repositoryRoot
try {
    & $dotnetExecutable restore "RoboCopy_GUI_Kr.sln" --configfile "NuGet.Config" --disable-parallel -m:1 -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE." }
    & $dotnetExecutable build "RoboCopy_GUI_Kr.sln" -c Release --no-restore -m:1
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE." }
    & $dotnetExecutable test "RoboCopy_GUI_Kr.sln" -c Release --no-build --no-restore -m:1
    if ($LASTEXITCODE -ne 0) { throw "dotnet test failed with exit code $LASTEXITCODE." }

    Assert-DevelopmentPath $developmentRoot
    if (Test-Path -LiteralPath $developmentRoot) {
        try {
            Remove-Item -LiteralPath $developmentRoot -Recurse -Force
        } catch [IO.IOException] {
            # An empty output directory can briefly remain locked after a crashed GUI process.
            # Clear each payload directory, but never overwrite loaded application files.
            foreach ($payloadDirectory in @($developmentAppRoot, $developmentTestRoot, $developmentToolRoot, $developmentSourceRoot)) {
                if (-not (Test-Path -LiteralPath $payloadDirectory)) { continue }
                try {
                    Remove-Item -LiteralPath $payloadDirectory -Recurse -Force
                } catch [IO.IOException] {
                    $remainingFiles = @(Get-ChildItem -LiteralPath $payloadDirectory -File -Recurse -ErrorAction SilentlyContinue)
                    if ($remainingFiles.Count -gt 0) {
                        throw "Development output is in use. Close the running development app and retry: $payloadDirectory"
                    }
                }
            }
        }
    }
    New-Item -ItemType Directory -Path $developmentAppRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $developmentTestRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $developmentToolRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $developmentSourceRoot -Force | Out-Null

    & $dotnetExecutable restore "src\RoboCopy_GUI_Kr\RoboCopy_GUI_Kr.csproj" -r win-x64 --configfile "NuGet.Config" --disable-parallel -m:1 -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw "development restore failed with exit code $LASTEXITCODE." }
    & $dotnetExecutable publish "src\RoboCopy_GUI_Kr\RoboCopy_GUI_Kr.csproj" -c Release -r win-x64 --no-restore -m:1 `
        --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o $developmentAppRoot
    if ($LASTEXITCODE -ne 0) { throw "development publish failed with exit code $LASTEXITCODE." }
    & $dotnetExecutable restore "src\RoboCopy_GUI_Kr.ElevatedWorker\RoboCopy_GUI_Kr.ElevatedWorker.csproj" -r win-x64 --configfile "NuGet.Config" --disable-parallel -m:1 -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw "elevated worker restore failed with exit code $LASTEXITCODE." }
    & $dotnetExecutable publish "src\RoboCopy_GUI_Kr.ElevatedWorker\RoboCopy_GUI_Kr.ElevatedWorker.csproj" -c Release -r win-x64 --no-restore -m:1 `
        --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o $developmentAppRoot
    if ($LASTEXITCODE -ne 0) { throw "elevated worker publish failed with exit code $LASTEXITCODE." }
    & $dotnetExecutable restore "src\RoboCopy_GUI_Kr.Agent\RoboCopy_GUI_Kr.Agent.csproj" -r win-x64 --configfile "NuGet.Config" --disable-parallel -m:1 -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw "agent restore failed with exit code $LASTEXITCODE." }
    & $dotnetExecutable publish "src\RoboCopy_GUI_Kr.Agent\RoboCopy_GUI_Kr.Agent.csproj" -c Release -r win-x64 --no-restore -m:1 `
        --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o $developmentAppRoot
    if ($LASTEXITCODE -ne 0) { throw "agent publish failed with exit code $LASTEXITCODE." }
    & $dotnetExecutable restore "src\RoboCopy_GUI_Kr.LockTestHelper\RoboCopy_GUI_Kr.LockTestHelper.csproj" -r win-x64 --configfile "NuGet.Config" --disable-parallel -m:1 -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw "lock test helper restore failed with exit code $LASTEXITCODE." }
    & $dotnetExecutable publish "src\RoboCopy_GUI_Kr.LockTestHelper\RoboCopy_GUI_Kr.LockTestHelper.csproj" -c Release -r win-x64 --no-restore -m:1 `
        --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o $developmentToolRoot
    if ($LASTEXITCODE -ne 0) { throw "lock test helper publish failed with exit code $LASTEXITCODE." }

    & (Join-Path $repositoryRoot "installer\Copy-DistributionNotices.ps1") -PublishDirectory $developmentAppRoot
    Register-DevelopmentExplorerMenu (Join-Path $developmentAppRoot "RoboCopy_GUI_Kr.exe")

    Copy-Item -Path (Join-Path $repositoryRoot "tests\RoboCopy_GUI_Kr.Core.Tests\bin\Release\net8.0\*") `
        -Destination $developmentTestRoot -Recurse -Force
    Copy-SourceSnapshot $developmentSourceRoot

    $developmentBytes = (Get-ChildItem -LiteralPath $developmentRoot -File -Recurse | Measure-Object Length -Sum).Sum
    Write-Host "Development package (self-contained app + test DLLs + full source): $developmentRoot"
    Write-Host "Development package size: $developmentBytes bytes"
} finally {
    Pop-Location
}
