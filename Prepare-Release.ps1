param()
$ErrorActionPreference = 'Stop'
$repositoryRoot = $PSScriptRoot
[xml]$properties = Get-Content -LiteralPath (Join-Path $repositoryRoot 'Directory.Build.props') -Raw
$packageVersion = [string]$properties.Project.PropertyGroup.Version
$displayVersion = [string]$properties.Project.PropertyGroup.InformationalVersion
$releaseRoot = Join-Path $repositoryRoot "artifacts/release/v$packageVersion"
$sourceName = "RoboCopy_GUI_Kr_v$packageVersion"
$snapshotRoot = Join-Path $releaseRoot "source/$sourceName"
$installerName = "RoboCopy_GUI_Kr_Setup_v${displayVersion}_x64.exe"
$installer = Join-Path $repositoryRoot "artifacts/installer/$installerName"
$zipName = "RoboCopy_GUI_Kr_Source_v$packageVersion.zip"
$zipPath = Join-Path $releaseRoot $zipName
if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) { throw "Build the installer first: $installer" }
New-Item -ItemType Directory -Path $snapshotRoot -Force | Out-Null

# Explicit source allowlist: never include local SDKs, artifacts or credentials.
$rootFiles = @('.gitattributes', '.gitignore', '.gitlab-ci.yml', 'AGENTS.md', 'Build.ps1',
    'Build-Installer.ps1', 'Prepare-Release.ps1', 'Directory.Build.props', 'DEVELOPMENT_STATUS.md',
    'DEVELOPMENT_TOOLS.md', 'global.json', 'NuGet.Config', 'README.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md',
    'RELEASING.md', 'Robocopy_GUI_개발계획서.md', 'RoboCopy_GUI_Kr.sln', 'Run-Development.ps1', 'Setup-LocalSdk.ps1')
$sourceFiles = @($rootFiles | ForEach-Object { Get-Item -LiteralPath (Join-Path $repositoryRoot $_) })
foreach ($tree in @('src', 'tests', 'installer', 'test-assets', '.github', 'docs')) {
    $sourceFiles += @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot $tree) -File -Recurse -Force |
        Where-Object {
            $_.FullName -notmatch '[\\/](bin|obj|TestResults|\.git|\.aws|\.codex|\.agents)[\\/]' -and
            $_.Name -notmatch '\.(pfx|p12|key|tmp|bak|old|log|user|suo)$' -and
            $_.Name -notmatch '_wpftmp\.csproj$' -and
            $_.Name -notmatch '^\.env($|\.)' -and
            $_.Name -notin @('.lock-process-id', '.lock-ready', '.release-lock') -and
            $_.FullName -notmatch '[\\/]test-assets[\\/].*[\\/]destination([\\/]|$)' -and
            $_.FullName -notmatch '[\\/]test-assets[\\/]elevated-retry[\\/](source|destination)([\\/]|$)'
        })
}
$sourceFiles = @($sourceFiles | Sort-Object FullName -Unique)
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipStream = [IO.File]::Open($zipPath, [IO.FileMode]::Create, [IO.FileAccess]::Write)
$archive = [IO.Compression.ZipArchive]::new($zipStream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $sourceFiles) {
        $prefix = $repositoryRoot.TrimEnd('\') + '\'
        if (-not $file.FullName.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Source escapes repository: $($file.FullName)"
        }
        $relative = $file.FullName.Substring($prefix.Length)
        $target = Join-Path $snapshotRoot $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $target,
            "$sourceName/$($relative.Replace('\', '/'))", [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose(); $zipStream.Dispose() }
Copy-Item -LiteralPath $installer -Destination (Join-Path $releaseRoot $installerName) -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot "docs/releases/v$packageVersion.md") -Destination (Join-Path $releaseRoot 'RELEASE_NOTES.md') -Force
$hashLines = foreach ($name in @($installerName, $zipName)) {
    "$((Get-FileHash -LiteralPath (Join-Path $releaseRoot $name) -Algorithm SHA256).Hash.ToLowerInvariant())  $name"
}
[IO.File]::WriteAllText((Join-Path $releaseRoot 'SHA256SUMS.txt'), ($hashLines -join "`r`n") + "`r`n",
    [Text.UTF8Encoding]::new($false))
Write-Output "Prepared $($sourceFiles.Count) source files and release assets: $releaseRoot"
