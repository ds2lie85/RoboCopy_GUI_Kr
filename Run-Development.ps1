$ErrorActionPreference = "Stop"
$repositoryRoot = $PSScriptRoot
$applicationPath = Join-Path $repositoryRoot "artifacts\development\RoboCopy_GUI_Kr_Development\app\RoboCopy_GUI_Kr.exe"

if (-not (Test-Path -LiteralPath $applicationPath)) {
    throw "Development build is missing. Run .\Build.ps1 first."
}

& $applicationPath
