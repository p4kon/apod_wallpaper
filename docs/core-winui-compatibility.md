# Core and WinUI Compatibility Audit

## Status

The target-framework migration pass is now **completed at the Core level**.

`apod_wallpaper.Core` now builds as a multi-targeted Windows backend:

- `net48`
- `net8.0-windows10.0.19041.0`

The backend can now be referenced by a modern Windows host without rewriting its public API.

## What Changed

The migration pass introduced:

- SDK-style project format for `apod_wallpaper.Core`
- dual-target build for current WinForms host and future WinUI host
- a dedicated `net8` compatibility probe project

Validated areas:

- `ApplicationController` construction and initialization under `net8`
- DPAPI secret store round-trip under `net8`
- `DisplayMetrics` screen bounds via Win32 under `net8`
- `SmartWallpaperComposer` resize/crop path under `net8`
- `WallpaperNative` registry and `SystemParametersInfo` execution path under `net8`
- `Network` `HttpWebRequest` fallback under `net8`
- `ApodPageImageExtractor` HTML parsing under `net8`

## Confirmed Compatibility Blockers

### 1. Core still uses `System.Drawing`

The backend currently relies on `System.Drawing` and `System.Drawing.Imaging` in multiple areas:

- image download and save pipeline
- smart wallpaper composition
- local image validation
- display size helpers

This path now builds and passes the probe under `net8.0-windows`, so it is **not an immediate blocker**.

It remains a future technical watchpoint if packaged WinUI runtime behavior or Store requirements later expose issues.

### 2. Core uses Windows-native desktop APIs

The backend also uses Windows-specific APIs and desktop integration points:

- registry access for wallpaper state/history
- `SystemParametersInfo`
- DPAPI (`ProtectedData`)

These are acceptable for a Windows-only host, but they mean the backend should be treated as a **Windows backend**, not as a neutral `netstandard` library.

### 3. Storage still needs packaged-host validation

Storage logic currently relies on:

- `Environment.SpecialFolder.LocalApplicationData`
- `AppDomain.CurrentDomain.BaseDirectory`
- portable marker and executable-adjacent paths

This is acceptable for the migration pass, but packaged WinUI behavior must still be validated in the PoC.

## What This Means For Target Strategy

### Recommended direction

The chosen direction is now implemented:

- keep `net48` for the current WinForms host
- add `net8.0-windows10.0.19041.0` for the future WinUI host

### Why not `netstandard2.0`

`netstandard2.0` is not the right target for the current backend because the backend is not platform-neutral:

- wallpaper apply is Windows-only
- registry access is Windows-only
- DPAPI is Windows-oriented
- current image path still uses GDI+/desktop-era APIs

So the right framing is:

> multi-targeted Windows backend

not:

> one portable `netstandard` backend

## Remaining Operational Caveat

The backend migration is complete, but the current local Visual Studio/MSBuild toolchain is older than the installed .NET 8 SDK requirement:

- installed SDK used by CLI: `.NET 8.0.420`
- local Visual Studio MSBuild: `17.5`
- minimum MSBuild required by SDK `8.0.420`: `17.8.3`

What this means in practice:

- `dotnet build` works for the `net8` target and compatibility probe
- the legacy `net48` path still builds and smoke tests still pass
- a one-shot full solution build through the current old Visual Studio MSBuild is not yet available on this machine until the IDE/build tools are updated

## Immediate Decision

The next frontend-related work should follow this order:

1. Keep the WinUI 3 host decision.
2. Treat the Core migration as complete.
3. Continue with the WinUI packaged PoC.
4. Validate packaged storage and capability requirements there.

## Bottom Line

The correct statement now is:

- the backend architecture is isolated enough for a new frontend
- the backend project targeting is now ready for direct Windows-host consumption
- the remaining unknowns are no longer Core targeting, but packaged-host behavior and local IDE toolchain freshness
