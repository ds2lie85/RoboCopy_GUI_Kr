param(
    [string]$LockFilePath
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$helperPath = Join-Path $repositoryRoot "artifacts\development\RoboCopy_GUI_Kr_Development\test-tools\RoboCopy_GUI_Kr.LockTestHelper.exe"

if (-not (Test-Path -LiteralPath $helperPath)) {
    throw "시험 도우미가 없습니다. 저장소 루트에서 build.ps1을 먼저 실행하세요."
}

if (-not [string]::IsNullOrWhiteSpace($LockFilePath)) {
    Start-Process -FilePath $helperPath -ArgumentList @([IO.Path]::GetFullPath($LockFilePath))
} else {
    Start-Process -FilePath $helperPath
}
