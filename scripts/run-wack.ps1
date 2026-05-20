param(
    [string]$PackagePath,
    [string]$ReportPath = "artifacts\wack\apod-wallpaper-wack.xml"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$appCertPath = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\App Certification Kit\appcert.exe"

if (-not (Test-Path -LiteralPath $appCertPath)) {
    throw "Windows App Certification Kit was not found at '$appCertPath'. Install the Windows SDK App Certification Kit first."
}

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $artifactRoot = Join-Path $repoRoot "artifacts"
    $latestPackage = Get-ChildItem -Path $artifactRoot -Recurse -File -Filter *.msix -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $latestPackage) {
        throw "No MSIX package was found under '$artifactRoot'. Build one with scripts\publish-msix.ps1 first."
    }

    $PackagePath = $latestPackage.FullName
}

$resolvedPackagePath = Resolve-Path -LiteralPath $PackagePath
$resolvedReportPath = Join-Path $repoRoot $ReportPath
$reportDirectory = Split-Path -Parent $resolvedReportPath
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null

& $appCertPath reset
& $appCertPath test -appxpackagepath $resolvedPackagePath -reportoutputpath $resolvedReportPath

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if (-not (Test-Path -LiteralPath $resolvedReportPath)) {
    throw "WACK finished but did not create the report at '$resolvedReportPath'. Try running this script from an elevated PowerShell session."
}

Get-Item -LiteralPath $resolvedReportPath | Select-Object FullName, Length, LastWriteTime
