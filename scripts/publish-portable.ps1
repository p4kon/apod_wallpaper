param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Version = "",
    [string]$OutputRoot = "",
    [switch]$SelfContained,
    [switch]$SkipInstaller,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot "..")
$projectPath = Join-Path $repoRoot "apod_wallpaper.WinUI\apod_wallpaper.WinUI.csproj"
$manifestPath = Join-Path $repoRoot "apod_wallpaper.WinUI\Package.appxmanifest"

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$manifest = Get-Content -LiteralPath $manifestPath
    $Version = $manifest.Package.Identity.Version
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "artifacts\release"
}

$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$repoRootPath = [System.IO.Path]::GetFullPath($repoRoot)
$isSelfContained = [bool]$SelfContained
$selfContainedText = $isSelfContained.ToString().ToLowerInvariant()

if ($Clean -and (Test-Path -LiteralPath $OutputRoot)) {
    $resolvedOutput = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $OutputRoot))
    if (-not $resolvedOutput.StartsWith($repoRootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean path outside repository: $resolvedOutput"
    }

    Remove-Item -LiteralPath $OutputRoot -Recurse -Force
}

$publishRoot = Join-Path $OutputRoot "_publish"
$publishFlavor = if ($isSelfContained) { "self-contained" } else { "framework-dependent" }
$publishTemp = Join-Path $publishRoot "$RuntimeIdentifier-$publishFlavor"
$portableName = if ($isSelfContained) {
    "APODWallpaper-$Version-$RuntimeIdentifier-self-contained-portable"
} else {
    "APODWallpaper-$Version-$RuntimeIdentifier-portable"
}
$portableDir = Join-Path $OutputRoot $portableName
$appDir = Join-Path $portableDir "app"
$zipPath = Join-Path $OutputRoot "$portableName.zip"
$shaPath = "$zipPath.sha256"

New-Item -ItemType Directory -Force -Path $publishTemp, $portableDir | Out-Null

$publishArgs = @(
    "publish", $projectPath,
    "-c", $Configuration,
    "-r", $RuntimeIdentifier,
    "--self-contained", $selfContainedText,
    "-o", $publishTemp,
    "/p:WindowsPackageType=None",
    "/p:WindowsAppSDKSelfContained=$selfContainedText",
    "/p:GenerateAppxPackageOnBuild=false",
    "/p:PublishReadyToRun=false",
    "/p:PublishSingleFile=false",
    "/p:DebugType=none",
    "/p:DebugSymbols=false"
)

Write-Host "Publishing portable build ($publishFlavor)..."
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if (Test-Path -LiteralPath $portableDir) {
    Remove-Item -LiteralPath $portableDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $portableDir, $appDir | Out-Null
$publishedFiles = @(Get-ChildItem -LiteralPath $publishTemp -Force -File)
if ($publishedFiles.Count -eq 0) {
    throw "Publish output is empty: $publishTemp"
}

robocopy $publishTemp $appDir /E /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -gt 7) {
    throw "robocopy failed with exit code $LASTEXITCODE"
}
Remove-Item -LiteralPath $publishRoot -Recurse -Force

$appExePath = Join-Path $appDir "apod_wallpaper.WinUI.exe"
if (-not (Test-Path -LiteralPath $appExePath)) {
    throw "Portable build is missing apod_wallpaper.WinUI.exe. Publish output path: $publishTemp"
}

$launcherSource = Join-Path $repoRoot "tools\PortableLauncher\APODWallpaperLauncher.cs"
$launcherOutput = Join-Path $portableDir "APODWallpaper.exe"
$cscCandidates = @(
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
    (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
)
$csc = $cscCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $csc) {
    throw "Cannot find .NET Framework C# compiler (csc.exe) for portable launcher."
}

$launcherArgs = @(
    "/nologo",
    "/target:winexe",
    "/out:$launcherOutput",
    "/reference:System.Windows.Forms.dll"
)
$iconPath = Join-Path $repoRoot "apod_wallpaper.WinUI\Assets\AppIcon.ico"
if (Test-Path -LiteralPath $iconPath) {
    $launcherArgs += "/win32icon:$iconPath"
}
$launcherArgs += $launcherSource

Write-Host "Building portable launcher..."
& $csc @launcherArgs
if ($LASTEXITCODE -ne 0) {
    throw "Portable launcher compilation failed with exit code $LASTEXITCODE"
}

Get-ChildItem -LiteralPath $portableDir -Recurse -File |
    Where-Object { $_.Extension -in ".pdb", ".appxrecipe" } |
    Remove-Item -Force

New-Item -ItemType File -Force -Path (Join-Path $portableDir "portable.mode") | Out-Null
New-Item -ItemType Directory -Force -Path `
    (Join-Path $portableDir "images"), `
    (Join-Path $portableDir "images\smart"), `
    (Join-Path $portableDir "data"), `
    (Join-Path $portableDir "data\cache"), `
    (Join-Path $portableDir "data\logs"), `
    (Join-Path $portableDir "data\secrets") | Out-Null

@"
APOD Wallpaper portable build

1. Extract the whole folder before running the app.
2. Start APODWallpaper.exe.
3. Internal app runtime files are stored in .\app.
4. Images are saved next to the launcher in .\images.
5. Smart wallpaper variants are saved in .\images\smart.
6. Settings, cache, logs, and local secrets are stored in .\data.

Build type: $publishFlavor

The default portable build is framework-dependent to keep the download small.
If the app does not start on a clean Windows machine, install:
- .NET 8 Desktop Runtime x64
- Windows App Runtime / Windows App SDK runtime

This build is not code-signed yet. Windows SmartScreen may show a warning until a trusted signing certificate is available.
"@ | Set-Content -LiteralPath (Join-Path $portableDir "README-FIRST.txt") -Encoding UTF8

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $portableDir "*") -DestinationPath $zipPath -Force
$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
"$($hash.Hash)  $(Split-Path -Leaf $zipPath)" | Set-Content -LiteralPath $shaPath -Encoding ASCII

$fileCount = (Get-ChildItem -LiteralPath $portableDir -Recurse -File).Count
Write-Host "Portable folder: $portableDir"
Write-Host "Portable zip:    $zipPath"
Write-Host "SHA256:          $shaPath"
Write-Host "Files:           $fileCount"

if (-not $SkipInstaller) {
    $installerScript = Join-Path $scriptRoot "build-installer.ps1"
    & $installerScript -PortableDir $portableDir -Version $Version -OutputRoot $OutputRoot
}
