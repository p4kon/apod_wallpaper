# Local MSIX smoke-test attempt

Date: 2026-05-20

## Package

```text
artifacts\msix-spike\apod_wallpaper.WinUI_1.0.0.0_x64_Test\apod_wallpaper.WinUI_1.0.0.0_x64.msix
```

## Result

Status:

```text
BLOCKED BEFORE INSTALL
```

The MSIX package builds and signs with a local test certificate, but Windows refuses installation until the test certificate is trusted as a root certificate.

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

Signed install attempt:

```text
Add-AppxPackage failed with 0x80073CF0 / 0x800B0109
The root certificate of the signature in the app package or bundle must be trusted.
```

The test certificate was imported into CurrentUser TrustedPeople, but Windows still requires the certificate chain root to be trusted.

Automated CurrentUser Root import was not completed from the non-interactive shell because Windows blocks/waits for root trust confirmation.

## Manual unblock

Import the local test certificate into Current User Trusted Root Certification Authorities:

```text
artifacts\cert\APODWallpaper-LocalTest.cer
```

Recommended UI path:

1. Double-click `APODWallpaper-LocalTest.cer`.
2. Click `Install Certificate`.
3. Choose `Current User`.
4. Choose `Place all certificates in the following store`.
5. Select `Trusted Root Certification Authorities`.
6. Finish and confirm the security warning.

After that, retry:

```powershell
$packageRoot = Resolve-Path '.\artifacts\msix-spike\apod_wallpaper.WinUI_1.0.0.0_x64_Test'
Add-AppxPackage `
  -Path (Join-Path $packageRoot 'apod_wallpaper.WinUI_1.0.0.0_x64.msix') `
  -DependencyPath (Join-Path $packageRoot 'Dependencies\x64\Microsoft.WindowsAppRuntime.2.msix') `
  -ForceApplicationShutdown `
  -ForceUpdateFromAnyVersion
```

## Smoke tests still pending

These were not executed because installation did not complete:

- launch packaged app
- tray icon / hide / restore / exit
- startup task enable / reboot check
- wallpaper apply from packaged app
- storage under package local app data
- update over older package version

## Packaging note

For repeatable Store/MSIX testing, the project needs a proper test-signing flow:

- stable dev certificate for local testing
- documented certificate import step
- package version bump for update testing
- later, real Store signing through Partner Center
