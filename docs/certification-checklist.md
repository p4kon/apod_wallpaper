# Certification and sensitive scenario checklist

Date: 2026-05-20

This checklist is for the Microsoft Store technical certification pass and manual validation of sensitive APOD Wallpaper scenarios.

Microsoft reference:

- Windows App Certification Kit: https://learn.microsoft.com/en-us/windows/uwp/debug-test-perf/windows-app-certification-kit

## Prerequisites

- Windows SDK App Certification Kit is installed.
- The app is built as MSIX with `scripts\publish-msix.ps1`.
- Tests are run in an active desktop user session.
- WACK is run from an elevated PowerShell or Command Prompt.
- Close Visual Studio/debuggers before running timing-sensitive certification tests.

## Build package

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-msix.ps1 -OutputDirectory artifacts\msix
```

Expected:

- MSIX package is created under `artifacts\msix`.
- Only acceptable local warning for now: missing `mspdbcmf.exe` for symbols package generation.

## Run WACK

From elevated PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-wack.ps1
```

Or with an explicit package:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-wack.ps1 -PackagePath "C:\path\to\apod_wallpaper.WinUI_1.0.0.0_x64.msix"
```

Expected:

- Report is created at `artifacts\wack\apod-wallpaper-wack.xml`.
- `OVERALL_RESULT` should be reviewed.
- Any `FAIL` entry must be triaged before Store submission.

Current local attempt:

- WACK tooling exists at `C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe`.
- Non-elevated CLI run did not create a report.
- This matches Microsoft guidance that WACK should be run from an elevated command window.

## Manual sensitive scenarios

### Startup

1. Install MSIX.
2. Launch the app.
3. Turn `Start with Windows` off.
4. Turn `Start with Windows` on.
5. Accept Windows startup permission prompt if shown.
6. Confirm the app appears in Windows Startup Apps.
7. Sign out/in or reboot.
8. Confirm the app starts automatically.
9. Confirm no duplicate startup entries are created.
10. Turn startup off and confirm Windows no longer starts the app.

### Tray

1. Launch app.
2. Confirm tray icon appears.
3. Click X while close-to-tray is enabled.
4. Confirm process stays alive.
5. Double-click tray icon and confirm the app restores.
6. Right-click tray icon and select Show.
7. Right-click tray icon and select Exit.
8. Repeat hide/show cycle 10 times.

### Wallpaper apply

1. Select an available image date.
2. Click Apply.
3. Confirm desktop wallpaper changes.
4. Test Smart, Fill, Fit, Stretch, Center, Tile, and Span.
5. Confirm style changes use the currently applied wallpaper source.
6. Apply a very wide image and confirm Smart mode behaves correctly.
7. Apply a portrait image and confirm Smart mode behaves correctly.
8. Reboot/sign out and confirm wallpaper persists.

### Automatic updates

1. Enable Auto On.
2. Confirm the app applies the latest available image when appropriate.
3. Confirm video days are marked as video and skipped.
4. Confirm unpublished dates are not applied.
5. Confirm already-applied current image is not re-applied repeatedly.
6. Confirm calendar color updates after automatic download/apply.

### Storage and update

1. Install version N.
2. Set a custom download folder.
3. Download at least one image.
4. Save settings and close app.
5. Install version N+1 over version N.
6. Confirm settings, downloaded images, metadata cache, and API key state survive.
7. Confirm logs are written under package local app data.

### API key

1. Add valid NASA API key.
2. Confirm key state is saved.
3. Remove key.
4. Confirm app still works through public APOD pages.
5. Confirm invalid key shows a clear message and does not break preview/apply.

### Network and APOD content

1. Load image day.
2. Load video day.
3. Load unpublished/future date.
4. Disable network and confirm the app does not crash.
5. Re-enable network and retry.

## Clean machine matrix

Minimum:

- Windows 10 22H2 x64.
- Windows 11 x64.
- Clean user profile.
- Machine without Visual Studio.
- Machine where previous portable version was used.

## Certification exit criteria

- WACK report exists and has no blocking failures.
- Manual startup, tray, wallpaper, storage, and update scenarios pass.
- No sensitive feature silently fails.
- Store metadata has privacy URL, support URL, screenshots, age rating, and capability justification ready.
