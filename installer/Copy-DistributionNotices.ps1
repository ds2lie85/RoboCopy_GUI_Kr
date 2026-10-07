param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory,
    [string]$PackageCacheDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) ".nuget\packages")
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$dependencyPath = Join-Path $PublishDirectory "RoboCopy_GUI_Kr.deps.json"
$dependencies = Get-Content -LiteralPath $dependencyPath -Raw -Encoding UTF8 | ConvertFrom-Json
$runtimePackages = @($dependencies.libraries.PSObject.Properties.Name |
    Where-Object { $_.StartsWith("runtimepack.", [StringComparison]::OrdinalIgnoreCase) })
if ($runtimePackages.Count -eq 0) {
    throw "Distribution notices must be collected from the self-contained application."
}

# Validate all inputs before copying; do not silently omit missing license files.
$noticeFiles = @()
foreach ($library in $runtimePackages) {
    if ($library -notmatch '^runtimepack\.([A-Za-z0-9][A-Za-z0-9_.-]+)/([0-9][A-Za-z0-9.+-]*)$') {
        throw "Invalid runtime package identity: $library"
    }
    $packageId = $Matches[1].ToLowerInvariant()
    $packageVersion = $Matches[2].ToLowerInvariant()
    $packageRoot = Join-Path $PackageCacheDirectory "$packageId\$packageVersion"
    $files = @(Get-ChildItem -LiteralPath $packageRoot -File |
        Where-Object { $_.Name -match '^(LICENSE(\.(TXT|MD))?|THIRD-PARTY-NOTICES(\.(TXT|MD))?)$' })
    if (@($files | Where-Object { $_.Name -match '^LICENSE' }).Count -eq 0) {
        throw "Runtime package license is missing: $packageId/$packageVersion"
    }
    if ($packageId -eq 'microsoft.netcore.app.runtime.win-x64' -and
        @($files | Where-Object { $_.Name -match '^THIRD-PARTY-NOTICES' }).Count -eq 0) {
        throw "Runtime third-party notices are missing: $packageId/$packageVersion"
    }
    foreach ($file in $files) {
        $noticeFiles += [PSCustomObject]@{
            Source = $file.FullName
            Destination = Join-Path $PublishDirectory "third-party\$packageId\$packageVersion\$($file.Name)"
        }
    }
}

foreach ($file in @("LICENSE", "README.md", "THIRD_PARTY_NOTICES.md")) {
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $file) -PathType Leaf)) {
        throw "Project distribution document is missing: $file"
    }
}
foreach ($file in @("LICENSE", "README.md", "THIRD_PARTY_NOTICES.md")) {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot $file) -Destination (Join-Path $PublishDirectory $file) -Force
}
foreach ($notice in $noticeFiles) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $notice.Destination) -Force | Out-Null
    Copy-Item -LiteralPath $notice.Source -Destination $notice.Destination -Force
}
