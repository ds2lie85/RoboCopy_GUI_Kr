$ErrorActionPreference = "Stop"

$testRoot = $PSScriptRoot
$sourcePath = Join-Path $testRoot "source"
$destinationPath = Join-Path $testRoot "destination"
$holderScript = Join-Path $testRoot "Hold-ExclusiveLock.ps1"
$readyFile = Join-Path $testRoot ".lock-ready"
$releaseFile = Join-Path $testRoot ".release-lock"
$processFile = Join-Path $testRoot ".lock-process-id"

if (Test-Path -LiteralPath $readyFile) {
    Write-Host "The lock test is already running."
    Write-Host "Source: $sourcePath"
    Write-Host "Destination: $destinationPath"
    exit 0
}

Remove-Item -LiteralPath $releaseFile -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $processFile -Force -ErrorAction SilentlyContinue

$powershellPath = (Get-Command powershell.exe -ErrorAction Stop).Source
$process = Start-Process -FilePath $powershellPath `
    -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$holderScript`"") `
    -WindowStyle Hidden `
    -PassThru

$deadline = [DateTime]::UtcNow.AddSeconds(10)
while (-not (Test-Path -LiteralPath $readyFile) -and [DateTime]::UtcNow -lt $deadline) {
    if ($process.HasExited) {
        throw "The lock holder exited before it was ready. Exit code: $($process.ExitCode)"
    }

    Start-Sleep -Milliseconds 100
}

if (-not (Test-Path -LiteralPath $readyFile)) {
    throw "The exclusive file lock was not ready within 10 seconds."
}

Write-Host "The copy-failure test is ready."
Write-Host "Source: $sourcePath"
Write-Host "Destination: $destinationPath"
Write-Host "Recommended: Retry count 0, retry wait 0."
Write-Host "Run Stop-Failure-Test.ps1 after the test to release the lock cleanly."
