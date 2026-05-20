# MSIX packaging checklist

## Build

Use the repository script:

```powershell
.\scripts\publish-msix.ps1
```

If PowerShell blocks local scripts because of execution policy, use:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-msix.ps1
```

Expected result:

- MSIX package is generated under `artifacts\msix`.
- A symbols warning about `mspdbcmf.exe` is acceptable for local smoke testing.
- The generated manifest keeps `EntryPoint="Windows.FullTrustApplication"`.
- The generated manifest includes `runFullTrust`.
- The generated manifest includes `windows.startupTask` with `TaskId="APODWallpaperStartupTask"`.

## Local install smoke test

1. Install the generated MSIX package locally.
2. Launch APOD Wallpaper from Start.
3. Confirm the app opens normally.
4. Confirm logs/settings/cache are created under the package local app data, not next to the executable.
5. Confirm preview loads for today's APOD.

## Certification smoke test

Run WACK from elevated PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-wack.ps1
```

Review:

- `artifacts\wack\apod-wallpaper-wack.xml`
- `OVERALL_RESULT`
- every `FAIL` entry

## Tray smoke test

1. Confirm tray icon appears after app launch.
2. Click X with close-to-tray enabled.
3. Confirm process remains alive.
4. Double-click tray icon and confirm window restores.
5. Right-click tray icon and test Show.
6. Right-click tray icon and test Exit.
7. Repeat hide/show 10 times.

## Wallpaper smoke test

1. Apply an available image.
2. Confirm wallpaper changes.
3. Switch each style: Smart, Fill, Fit, Stretch, Center, Tile, Span.
4. Confirm the currently applied wallpaper is reused when changing style.
5. Sign out/in or reboot and confirm wallpaper persists.

## Startup smoke test

1. Open Settings.
2. Turn Start with Windows off.
3. Turn Start with Windows on.
4. Accept the Windows startup permission prompt if shown.
5. Confirm the app appears in Windows startup apps.
6. Reboot or sign out/in.
7. Confirm APOD Wallpaper starts automatically.
8. If startup does not happen, check Windows startup settings and package logs.

## Update smoke test

1. Install version N.
2. Change settings and download/apply at least one image.
3. Install version N+1 over it.
4. Confirm settings, cache metadata, and downloaded images still exist.
5. Confirm startup setting did not silently reset.

## Clean machine test

Run on a machine without Visual Studio and without repo-local build outputs.

Minimum matrix:

- Windows 10 22H2 x64.
- Windows 11 x64.
- Fresh user profile where APOD Wallpaper has never been installed.
- Upgrade from an older APOD Wallpaper MSIX.
