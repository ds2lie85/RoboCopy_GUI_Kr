$ErrorActionPreference = "Stop"

$testRoot = $PSScriptRoot
$readyFile = Join-Path $testRoot ".lock-ready"
$releaseFile = Join-Path $testRoot ".release-lock"
$processFile = Join-Path $testRoot ".lock-process-id"

if (-not (Test-Path -LiteralPath $readyFile)) {
    Write-Host "No lock test is running."
    exit 0
}

Set-Content -LiteralPath $releaseFile -Value "release" -Encoding Ascii
$deadline = [DateTime]::UtcNow.AddSeconds(10)
while ((Test-Path -LiteralPath $readyFile) -and [DateTime]::UtcNow -lt $deadline) {
    Start-Sleep -Milliseconds 100
}

if (Test-Path -LiteralPath $readyFile) {
    throw "The lock holder did not respond to the clean release signal. It was not force-terminated."
}

Remove-Item -LiteralPath $releaseFile -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $processFile -Force -ErrorAction SilentlyContinue
Write-Host "The file lock was released cleanly."
