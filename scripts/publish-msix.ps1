param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OutputDirectory = "artifacts\msix"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "apod_wallpaper.WinUI\apod_wallpaper.WinUI.csproj"
$outputPath = Join-Path $repoRoot $OutputDirectory

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

dotnet msbuild $projectPath `
    /t:Restore,Publish `
    /p:Configuration=$Configuration `
    /p:Platform=$Platform `
    /p:RuntimeIdentifier=$RuntimeIdentifier `
    /p:GenerateAppxPackageOnBuild=true `
    /p:AppxBundle=Never `
    /p:AppxPackageSigningEnabled=false `
    /p:UapAppxPackageBuildMode=SideloadOnly `
    /p:AppxPackageDir="$outputPath\"

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Get-ChildItem -Path $outputPath -Recurse -Filter *.msix |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 5 FullName, Length, LastWriteTime
