# Microsoft Store / MSIX compatibility spike

Date: 2026-05-20

## Goal

Confirm whether the WinUI frontend can keep the core product scenarios in packaged / MSIX mode:

- tray icon
- wallpaper apply
- startup with Windows
- storage layout

This is a technical compatibility spike, not a final Store submission checklist.

Microsoft references used:

- Packaged desktop app startup extension: https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/desktop-to-uwp-extensions#start-an-executable-file-when-users-log-into-windows
- `StartupTask` API behavior: https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.startuptask
- Restricted capability review process: https://learn.microsoft.com/en-us/windows/uwp/packaging/app-capability-declarations#restricted-capabilities
- Packaged desktop app registry / filesystem behavior: https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes

## Local package generation

Command used:

```powershell
dotnet msbuild .\apod_wallpaper.WinUI\apod_wallpaper.WinUI.csproj `
  /t:Restore,Publish `
  /p:Configuration=Release `
  /p:Platform=x64 `
  /p:RuntimeIdentifier=win-x64 `
  /p:GenerateAppxPackageOnBuild=true `
  /p:AppxBundle=Never `
  /p:AppxPackageSigningEnabled=false `
  /p:UapAppxPackageBuildMode=SideloadOnly
```

Result:

- PASS: MSIX package generation succeeds.
- WARNING: `mspdbcmf.exe` was not found, so symbols package generation was skipped. This does not block the app package itself.
- The generated manifest uses `EntryPoint="Windows.FullTrustApplication"`.
- The generated manifest includes `runFullTrust`.
- Experiment: removing `runFullTrust` makes package generation fail with MakeAppx error `The element or attribute or attribute value specified requires "runFullTrust" capability.`

## Tray

Current implementation:

- `TrayIconController` uses Win32 APIs directly:
- `Shell_NotifyIcon`
- `ShowWindow`
- `TrackPopupMenu`
- window subclassing through `SetWindowLongPtr`

Assessment:

- Likely PASS for packaged full-trust desktop apps.
- Needs real installed-package runtime test:
- launch packaged app
- verify tray icon appears
- hide to tray
- double-click restores
- context menu Show / Exit works
- repeat 10 hide/show cycles

Store risk:

- Depends on `runFullTrust`, which is a restricted capability and must be justified during Store submission.

## Wallpaper apply

Current implementation:

- Primary path uses COM `IDesktopWallpaper`.
- Fallback path writes `HKCU\Control Panel\Desktop` wallpaper style values and calls `SystemParametersInfo(SPI_SETDESKWALLPAPER)`.

Assessment:

- Likely PASS for packaged full-trust desktop apps.
- Needs real installed-package runtime test because Store-packaged full-trust apps can still hit policy / capability review issues.

Required packaged test:

- apply normal image
- apply Smart image
- switch Fill / Fit / Stretch / Center / Tile / Span
- reboot or sign out/in and confirm wallpaper persists

Store risk:

- This is a system personalization feature. It should be clearly explained in Store listing and privacy/support text.
- No extra image capability is expected if files are stored in app-owned storage or user-selected folder, but Store review may still inspect behavior because it changes user desktop state.

## Startup with Windows

Current implementation:

- Unpackaged / portable mode writes to:
- `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- Packaged mode currently no-ops in `StoreStartupRegistrationService` when package identity exists.

Assessment:

- FAIL for packaged mode today.

Reason:

- MSIX / packaged startup should use a manifest startup task and `Windows.ApplicationModel.StartupTask`, not portable registry registration.

Required Store-specific work:

- Add `windows.startupTask` extension to `Package.appxmanifest`.
- Add packaged startup registration based on `StartupTask.GetAsync(...)` and `RequestEnableAsync()`.
- Define UX for startup request denied / disabled by policy / disabled by user in Windows settings.

## Storage

Current implementation:

- `BackendHost.ConfigureStorageLayout()` tries `ApplicationData.Current.LocalFolder`.
- If that fails, it falls back to portable `data` next to the executable.

Assessment:

- PASS for packaged mode design.
- In packaged mode, settings/logs/cache should go to app local data.
- In portable mode, settings/logs/cache go next to the exe.

Risks:

- User-selected image download folder must remain accessible after restart.
- Store package updates must not delete app local data.
- If the user stores images outside app data, folder picker access and direct filesystem paths need a test pass.

## Manifest observations

Current source manifest includes:

- `runFullTrust`

Notes:

- `runFullTrust` is required for the current desktop implementation because the generated package is a `Windows.FullTrustApplication`. Removing it fails package validation.
- `systemAIModels` was unrelated to current APOD Wallpaper functionality and should stay out unless a concrete feature needs it.

## Current verdict

Packaged/MSIX direction is viable, but not Store-ready yet.

Status by area:

- Tray: PROBABLE PASS, needs installed-package runtime test.
- Wallpaper apply: PROBABLE PASS, needs installed-package runtime test.
- Startup: IMPLEMENTED FOR PACKAGED MODE, needs installed-package runtime test.
- Storage: PASS by design, needs user-folder persistence test.
- Store review: RISK because of `runFullTrust`; prepare justification.

## Recommended next tasks

1. Install local MSIX test package and run tray / wallpaper / storage / startup tests.
2. Document Store capability justification for `runFullTrust`.
3. Verify Store submission metadata and privacy/support text.
