# Local MSIX smoke-test attempt

Date: 2026-05-20

## Package

```text
artifacts\msix-spike\apod_wallpaper.WinUI_1.0.0.0_x64_Test\apod_wallpaper.WinUI_1.0.0.0_x64.msix
```

## Result

Status:

```text
PASS WITH STARTUP REBOOT CHECK PENDING
```

The MSIX package builds, signs with a local test certificate, installs, updates over an older package version, launches, writes package-local data, supports tray UX, and applies wallpapers from the packaged app. Startup registration still needs a reboot check.

## Details

Initial unsigned install attempt:

```text
Add-AppxPackage -AllowUnsigned failed with 0x80073D2C
Deployment failed because the package publisher is not in the unsigned namespace.
```

Local test certificate created:

```text
Subject: CN=AppPublisher
Thumbprint: 6B58073E8CDEEB23A9B4D9569C322CB2BE6CBAAC
```

MSIX signing:

```text
signtool sign succeeded.
```

First signed install attempt:

```text
Add-AppxPackage failed with 0x80073CF0 / 0x800B0109
The root certificate of the signature in the app package or bundle must be trusted.
```

The test certificate was imported into CurrentUser TrustedPeople, but Windows still required the certificate chain root to be trusted for MSIX deployment.

Final certificate placement used for successful install:

```text
Cert:\LocalMachine\TrustedPeople
```

Manual command used from elevated PowerShell:

```powershell
Import-Certificate `
  -FilePath "C:\Users\p4kon\Documents\GitHub\apod_wallpaper\artifacts\cert\APODWallpaper-LocalTest.cer" `
  -CertStoreLocation "Cert:\LocalMachine\TrustedPeople"
```

Install command:

```powershell
$packageRoot = Resolve-Path '.\artifacts\msix-spike\apod_wallpaper.WinUI_1.0.0.0_x64_Test'
Add-AppxPackage `
  -Path (Join-Path $packageRoot 'apod_wallpaper.WinUI_1.0.0.0_x64.msix') `
  -DependencyPath (Join-Path $packageRoot 'Dependencies\x64\Microsoft.WindowsAppRuntime.2.msix') `
  -ForceApplicationShutdown `
  -ForceUpdateFromAnyVersion
```

Installed package:

```text
Name: DBC3583C-3BE3-4B57-8C61-672FB5F6E9A5
PackageFullName: DBC3583C-3BE3-4B57-8C61-672FB5F6E9A5_1.0.0.0_x64__1z32rh13vfry6
Publisher: CN=AppPublisher
Version: 1.0.0.0
InstallLocation: C:\Program Files\WindowsApps\DBC3583C-3BE3-4B57-8C61-672FB5F6E9A5_1.0.0.0_x64__1z32rh13vfry6
```

Launch command:

```powershell
explorer.exe shell:AppsFolder\DBC3583C-3BE3-4B57-8C61-672FB5F6E9A5_1z32rh13vfry6!App
```

Launch result:

```text
ProcessName: apod_wallpaper.WinUI
Result: PASS
```

## Update-over-old-version check

The first installed package was:

```text
DBC3583C-3BE3-4B57-8C61-672FB5F6E9A5_1.0.0.0_x64__1z32rh13vfry6
```

For update testing only, `Package.appxmanifest` was temporarily changed to `1.0.0.1`, a new MSIX was built under `artifacts\msix-update-v101`, and the manifest was immediately restored back to `1.0.0.0` in the working tree.

Updated package:

```text
artifacts\msix-update-v101\apod_wallpaper.WinUI_1.0.0.1_x64_Test\apod_wallpaper.WinUI_1.0.0.1_x64.msix
```

Update install command:

```powershell
Add-AppxPackage `
  -Path $mainPackage `
  -DependencyPath $dependencies `
  -ForceApplicationShutdown `
  -ForceUpdateFromAnyVersion
```

Update result:

```text
PackageFullName: DBC3583C-3BE3-4B57-8C61-672FB5F6E9A5_1.0.0.1_x64__1z32rh13vfry6
Result: PASS
```

Launch after update:

```text
ProcessName: apod_wallpaper.WinUI
MainWindowTitle: APOD Wallpaper
Result: PASS
```

Package-local storage observed:

```text
C:\Users\p4kon\AppData\Local\Packages\DBC3583C-3BE3-4B57-8C61-672FB5F6E9A5_1z32rh13vfry6\LocalState
```

Created package-local paths:

```text
LocalState\cache
LocalState\cache\previews
LocalState\cache\apod-metadata.json
LocalState\logs
```

## Manual smoke-test results

Verified in the installed MSIX package:

- Tray: close-to-tray, tray icon, restore, and exit work.
- Wallpaper apply: applying a selected image works.
- Calendar state: applied image is marked as local.
- Auto-check behavior: applying an older selected date disables auto-check as expected.
- Auto-check recovery: enabling auto-check again applies the latest available image.

Still pending:

- Startup task reboot check: startup toggle was not reboot-tested in this MSIX run.

## Packaging note

For repeatable Store/MSIX testing, the project needs a proper test-signing flow:

- stable dev certificate for local testing
- documented certificate import step into `Cert:\LocalMachine\TrustedPeople`
- package version bump for update testing
- later, real Store signing through Partner Center
