$ErrorActionPreference = "Stop"

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "This setup changes ACLs inside the test-assets folder. Run this script from an Administrator PowerShell window."
}

$testRoot = $PSScriptRoot
$sourceRoot = Join-Path $testRoot "source"
$destinationRoot = Join-Path $testRoot "destination"
$protectedRelativePath = "$([char]0xAD00)$([char]0xB9AC)$([char]0xC790) $([char]0xC804)$([char]0xC6A9) $([char]0xD3F4)$([char]0xB354)"
$fileRetryRelativePath = "$([char]0xD30C)$([char]0xC77C) $([char]0xAD8C)$([char]0xD55C) $([char]0xC2DC)$([char]0xD5D8)"
$normalFileName = "$([char]0xC77C)$([char]0xBC18) $([char]0xD30C)$([char]0xC77C).txt"
$retryFileName = "$([char]0xAD8C)$([char]0xD55C) $([char]0xC7AC)$([char]0xC2DC)$([char]0xB3C4) $([char]0xD30C)$([char]0xC77C).txt"
$sourceProtectedRoot = Join-Path $sourceRoot $protectedRelativePath
$destinationProtectedRoot = Join-Path $destinationRoot $protectedRelativePath
$sourceFileRetryRoot = Join-Path $sourceRoot $fileRetryRelativePath
$destinationFileRetryRoot = Join-Path $destinationRoot $fileRetryRelativePath
$sourceFileRetryPath = Join-Path $sourceFileRetryRoot $retryFileName
$destinationFileRetryPath = Join-Path $destinationFileRetryRoot $retryFileName

New-Item -ItemType Directory -Path $sourceProtectedRoot -Force | Out-Null
New-Item -ItemType Directory -Path $destinationProtectedRoot -Force | Out-Null
New-Item -ItemType Directory -Path $sourceFileRetryRoot -Force | Out-Null
New-Item -ItemType Directory -Path $destinationFileRetryRoot -Force | Out-Null

Set-Content -LiteralPath (Join-Path $sourceRoot $normalFileName) `
    -Value "This file should be copied without elevation." -Encoding UTF8
Set-Content -LiteralPath (Join-Path $sourceProtectedRoot $retryFileName) `
    -Value "This file exercises an elevated directory retry." -Encoding UTF8
Set-Content -LiteralPath $sourceFileRetryPath `
    -Value "This newer source file exercises an elevated file retry." -Encoding UTF8
Set-Content -LiteralPath $destinationFileRetryPath `
    -Value "This protected destination file must be replaced by the elevated retry." -Encoding UTF8

$propagation = [Security.AccessControl.PropagationFlags]::None
$allow = [Security.AccessControl.AccessControlType]::Allow
$systemSid = [Security.Principal.SecurityIdentifier]::new("S-1-5-18")
$administratorsSid = [Security.Principal.SecurityIdentifier]::new("S-1-5-32-544")

function Set-AdministratorOnlyAcl([string]$path, [bool]$isDirectory) {
    $acl = Get-Acl -LiteralPath $path
    $acl.SetAccessRuleProtection($true, $false)
    $inheritance = if ($isDirectory) {
        [Security.AccessControl.InheritanceFlags]::ContainerInherit -bor `
            [Security.AccessControl.InheritanceFlags]::ObjectInherit
    } else {
        [Security.AccessControl.InheritanceFlags]::None
    }

    foreach ($sid in @($systemSid, $administratorsSid)) {
        $rule = [Security.AccessControl.FileSystemAccessRule]::new(
            $sid,
            [Security.AccessControl.FileSystemRights]::FullControl,
            $inheritance,
            $propagation,
            $allow)
        $acl.AddAccessRule($rule)
    }

    Set-Acl -LiteralPath $path -AclObject $acl
}

Set-AdministratorOnlyAcl $destinationProtectedRoot $true
Set-AdministratorOnlyAcl $destinationFileRetryPath $false

Write-Host "The elevated-retry test environment is ready."
Write-Host "Source: $sourceRoot"
Write-Host "Destination: $destinationRoot"
Write-Host "Scenarios: one protected directory and one protected destination file."
Write-Host "After testing, run Reset-ElevatedRetry-Test.ps1 from an elevated PowerShell window."
