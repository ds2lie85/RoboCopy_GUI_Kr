param(
    [string]$SdkVersion = "8.0.425"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = $PSScriptRoot
$installDirectory = Join-Path $repositoryRoot ".dotnet"
$dotnetExecutable = Join-Path $installDirectory "dotnet.exe"

if (Test-Path -LiteralPath $dotnetExecutable) {
    $installedVersion = & $dotnetExecutable --version
    if ($installedVersion -eq $SdkVersion) {
        Write-Host ".NET SDK $SdkVersion is already ready in $installDirectory"
        exit 0
    }
}

$installerPath = Join-Path ([IO.Path]::GetTempPath()) "RoboCopy_GUI_Kr-dotnet-install.ps1"
Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -OutFile $installerPath
& $installerPath -Version $SdkVersion -Architecture "x64" -InstallDir $installDirectory -NoPath

Write-Host "Local .NET SDK is ready. Run .\Build.ps1 next."
