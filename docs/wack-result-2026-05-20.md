# WACK result

Date: 2026-05-20

Package:

```text
artifacts\msix-spike\apod_wallpaper.WinUI_1.0.0.0_x64_Test\apod_wallpaper.WinUI_1.0.0.0_x64.msix
```

Report:

```text
artifacts\wack\apod-wallpaper-wack.xml
```

## Summary

```text
OVERALL_RESULT=PASS
PASS tests: 23
FAIL tests: 1
```

The single failed test is optional:

```text
Requirement: Package sanity check
Test: Blocked executables
Optional: TRUE
Result: FAIL
Messages: 34
```

## What was fixed during triage

Two app-side process-launch references were removed before the final report was regenerated:

- Settings `Open folder` now uses `StorageFolder.GetFolderFromPathAsync(...)` and `Launcher.LaunchFolderAsync(...)` instead of `Process.Start(... UseShellExecute=true)`.
- Portable startup path fallback now uses `Assembly.GetEntryAssembly()?.Location` instead of `Process.GetCurrentProcess().MainModule?.FileName`.

`dotnet build .\apod_wallpaper.WinUI\apod_wallpaper.WinUI.csproj -c Debug -p:Platform=x64` passes after these changes.

## Remaining optional failure

WACK still reports blocked executable / process launch references from packaged runtime binaries, including examples such as:

- `System.Diagnostics.Process.dll`
- `coreclr.dll`
- `Microsoft.WindowsAppRuntime.Bootstrap.dll`
- `DirectML.dll`
- `Microsoft.Windows.SDK.NET.dll`
- `System.Private.CoreLib.dll`
- `WinRT.Runtime.dll`

It also still reports:

```text
apod_wallpaper.WinUI.exe contains shell32.dll!ShellExecuteW
apod_wallpaper.WinUI.dll contains System.Diagnostics.Process.Start
```

No direct `Process.Start`, `UseShellExecute`, or `GetCurrentProcess` usage remains in APOD Wallpaper source after triage.

## Interpretation

Current WACK status is acceptable for continuing Store preparation because the overall result is `PASS`.

The optional failure should still be kept as a Store review risk item because the package is a full-trust self-contained desktop app and includes runtime binaries that WACK scans aggressively.

## Follow-up

- Re-run WACK after any packaging/runtime changes.
- If Store review rejects this optional test, investigate framework-dependent MSIX packaging or trimming/runtime exclusion options.
- Keep manual smoke tests for tray, startup, wallpaper apply, storage, and update flow as separate release blockers.
