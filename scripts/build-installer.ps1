param(
    [Parameter(Mandatory = $true)]
    [string]$PortableDir,
    [string]$Version = "1.0.0",
    [string]$OutputRoot = ""
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot "..")

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "artifacts\release"
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot = Join-Path (Get-Location) $OutputRoot
}

$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)

$PortableDir = [System.IO.Path]::GetFullPath($PortableDir)
if (-not (Test-Path -LiteralPath $PortableDir)) {
    throw "Portable directory does not exist: $PortableDir"
}

$exe = Get-ChildItem -LiteralPath $PortableDir -Filter "APODWallpaper.exe" -File | Select-Object -First 1
if (-not $exe) {
    throw "Cannot find portable launcher APODWallpaper.exe in portable directory: $PortableDir"
}

$iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1
if (-not $iscc) {
    $knownPaths = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    $iscc = $knownPaths | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if (-not $iscc) {
    Write-Warning "Inno Setup 6 was not found. Portable build is ready, but setup.exe was not created."
    Write-Warning "Install Inno Setup 6 and rerun: .\scripts\build-installer.ps1 -PortableDir `"$PortableDir`" -Version `"$Version`""
    return
}

$installerRoot = Join-Path $OutputRoot "installer"
$setupOutput = Join-Path $OutputRoot "setup"
$prerequisitesDir = Join-Path $OutputRoot "prerequisites"
New-Item -ItemType Directory -Force -Path $installerRoot, $setupOutput, $prerequisitesDir | Out-Null

$windowsAppRuntimeVersion = "2.0.1"
$windowsAppRuntimeInstallerName = "WindowsAppRuntimeInstall-x64-$windowsAppRuntimeVersion.exe"
$windowsAppRuntimeInstallerUrl = "https://aka.ms/windowsappsdk/2.0/2.0.1/windowsappruntimeinstall-x64.exe"
$windowsAppRuntimeInstaller = Join-Path $prerequisitesDir $windowsAppRuntimeInstallerName
if (-not (Test-Path -LiteralPath $windowsAppRuntimeInstaller)) {
    Write-Host "Downloading Windows App Runtime $windowsAppRuntimeVersion x64..."
    Invoke-WebRequest -Uri $windowsAppRuntimeInstallerUrl -OutFile $windowsAppRuntimeInstaller
}

$issPath = Join-Path $installerRoot "apod-wallpaper.iss"
$safePortableDir = $PortableDir.TrimEnd("\")
$exeName = $exe.Name

@"
#define MyAppName "APOD Wallpaper"
#define MyAppVersion "$Version"
#define MyAppPublisher "p4kon"
#define MyAppURL "https://apod_wallpaper.p4kon.com"
#define MyAppExeName "$exeName"
#define WindowsAppRuntimeInstaller "$windowsAppRuntimeInstaller"
#define WindowsAppRuntimeInstallerFile "WindowsAppRuntimeInstall-x64.exe"

[Setup]
AppId={{7C7C4C74-8F80-4D11-9F8B-7A8A7C5A4D22}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL=https://github.com/p4kon/apod_wallpaper/releases/latest
DefaultDirName={localappdata}\Programs\APOD Wallpaper
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=$setupOutput
OutputBaseFilename=APODWallpaper-$Version-win-x64-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter=*.exe
RestartApplications=no
SetupIconFile=$repoRoot\apod_wallpaper.WinUI\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "$safePortableDir\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
Source: "{#WindowsAppRuntimeInstaller}"; DestDir: "{tmp}"; DestName: "{#WindowsAppRuntimeInstallerFile}"; Flags: deleteafterinstall

[Icons]
Name: "{autoprograms}\APOD Wallpaper"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{tmp}\{#WindowsAppRuntimeInstallerFile}"; Parameters: "--quiet"; StatusMsg: "Installing Windows App Runtime..."; Flags: waituntilterminated runhidden
Filename: "{app}\{#MyAppExeName}"; Description: "Launch APOD Wallpaper"; Flags: nowait postinstall skipifsilent
"@ | Set-Content -LiteralPath $issPath -Encoding UTF8

& $iscc $issPath
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE"
}

Write-Host "Installer output: $setupOutput"
