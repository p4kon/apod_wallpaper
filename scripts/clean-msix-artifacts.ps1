param(
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [System.IO.Path]::GetFullPath((Resolve-Path (Join-Path $scriptRoot "..")))

$targets = @(
    "artifacts\msix-store-upload-v1100-desktop",
    "artifacts\msix",
    "apod_wallpaper.WinUI\AppPackages"
)

foreach ($relative in $targets) {
    $path = Join-Path $repoRoot $relative
    if (-not (Test-Path -LiteralPath $path)) {
        continue
    }

    $resolved = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $path))
    if (-not $resolved.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove path outside repository: $resolved"
    }

    if ($WhatIf) {
        Write-Host "Would remove: $resolved"
    }
    else {
        Remove-Item -LiteralPath $resolved -Recurse -Force
        Write-Host "Removed: $resolved"
    }
}
