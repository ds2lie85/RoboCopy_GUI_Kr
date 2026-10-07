$ErrorActionPreference = "Stop"

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this reset script from an Administrator PowerShell window."
}

$testRoot = $PSScriptRoot
$protectedRelativePath = "$([char]0xAD00)$([char]0xB9AC)$([char]0xC790) $([char]0xC804)$([char]0xC6A9) $([char]0xD3F4)$([char]0xB354)"
$fileRetryRelativePath = "$([char]0xD30C)$([char]0xC77C) $([char]0xAD8C)$([char]0xD55C) $([char]0xC2DC)$([char]0xD5D8)"
$retryFileName = "$([char]0xAD8C)$([char]0xD55C) $([char]0xC7AC)$([char]0xC2DC)$([char]0xB3C4) $([char]0xD30C)$([char]0xC77C).txt"
$destinationProtectedRoot = Join-Path (Join-Path $testRoot "destination") $protectedRelativePath
$destinationFileRetryPath = Join-Path (Join-Path (Join-Path $testRoot "destination") $fileRetryRelativePath) $retryFileName

$restored = 0
foreach ($path in @($destinationFileRetryPath, $destinationProtectedRoot)) {
    if (-not (Test-Path -LiteralPath $path)) {
        continue
    }

    $protectedAcl = Get-Acl -LiteralPath $path
    $protectedAcl.SetAccessRuleProtection($false, $true)
    Set-Acl -LiteralPath $path -AclObject $protectedAcl
    $restored++
}

Write-Host "Inherited permissions were restored on $restored test item(s)."
Write-Host "You can remove test results manually if needed: $(Join-Path $testRoot 'destination')"
