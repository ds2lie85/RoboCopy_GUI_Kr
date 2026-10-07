$ErrorActionPreference = "Stop"

$testRoot = $PSScriptRoot
$lockedName = "$([char]0xC7A0)$([char]0xAE34) $([char]0xD30C)$([char]0xC77C).txt"
$lockedFile = Join-Path (Join-Path $testRoot "source") $lockedName
$readyFile = Join-Path $testRoot ".lock-ready"
$releaseFile = Join-Path $testRoot ".release-lock"
$processFile = Join-Path $testRoot ".lock-process-id"
$stream = $null

try {
    Remove-Item -LiteralPath $readyFile -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $releaseFile -Force -ErrorAction SilentlyContinue
    $stream = [IO.File]::Open($lockedFile, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
    Set-Content -LiteralPath $processFile -Value $PID -Encoding Ascii
    Set-Content -LiteralPath $readyFile -Value "ready" -Encoding Ascii

    while (-not (Test-Path -LiteralPath $releaseFile)) {
        Start-Sleep -Milliseconds 200
    }
}
finally {
    if ($null -ne $stream) {
        $stream.Dispose()
    }

    Remove-Item -LiteralPath $readyFile -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $releaseFile -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $processFile -Force -ErrorAction SilentlyContinue
}
