# Core and WinUI Compatibility Audit

## Status

The current `apod_wallpaper.Core` project is **not yet directly compatible** with a WinUI 3 host.

This is not a speculative concern. It is confirmed by the current project structure and framework targeting.

## Current Target Framework

`apod_wallpaper.Core` currently targets:

- `.NET Framework 4.8` (`net48`)

This means the current Core project cannot be referenced directly by a standard modern WinUI 3 packaged app project, which uses a modern .NET Windows target.

## Conclusion

Before building the real WinUI 3 frontend host, the backend requires a dedicated **target framework migration pass**.

This migration is not optional.

## Confirmed Compatibility Blockers

### 1. Current target is `net48`

The project file currently targets classic .NET Framework:

- `TargetFrameworkVersion = v4.8`

That alone blocks direct consumption from a standard WinUI 3 host project.

### 2. Core uses `System.Drawing`

The backend currently relies on `System.Drawing` and `System.Drawing.Imaging` in multiple areas:

- image download and save pipeline
- smart wallpaper composition
- local image validation
- display size helpers

This means a simple target-framework flip is not enough. The graphics path must be reviewed for modern Windows compatibility.

### 3. Core uses Windows-native desktop APIs

The backend also uses Windows-specific APIs and desktop integration points:

- registry access for wallpaper state/history
- `SystemParametersInfo`
- DPAPI (`ProtectedData`)

These are acceptable for a Windows-only host, but they mean the backend should be treated as a **Windows backend**, not as a neutral `netstandard` library.

### 4. Storage currently assumes classic desktop semantics

Storage logic currently relies on:

- `Environment.SpecialFolder.LocalApplicationData`
- `AppDomain.CurrentDomain.BaseDirectory`
- portable marker and executable-adjacent paths

This may still work under a packaged host, but it must be validated explicitly. It is not yet guaranteed.

## What This Means For Target Strategy

### Recommended direction

The backend should move to **multi-targeting**, but not to `netstandard2.0`.

Recommended target strategy:

- keep `net48` temporarily for the current WinForms host
- add a modern Windows target for the future host

Example direction:

- `net48`
- `net8.0-windows10.0.19041.0`

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

## Required Migration Work

The migration pass should explicitly cover:

1. Convert `apod_wallpaper.Core` to a modern SDK-style project file.
2. Introduce multi-targeting for:
   - current `net48` host
   - future WinUI host
3. Revalidate all `System.Drawing` usage under the modern Windows target.
4. Decide whether `System.Drawing` remains acceptable for the Windows-only path or whether image composition should move to a newer imaging stack later.
5. Revalidate packaged storage behavior for settings, logs, cache, secrets, and smart wallpaper output.

## Immediate Decision

The next frontend-related work should follow this order:

1. Keep the WinUI 3 host decision.
2. Keep the WinUI PoC as the host/platform validation step.
3. Add a **separate required Core migration task**:
   - modernize framework targeting
   - keep public API stable
   - avoid breaking the current WinForms host during transition

## Bottom Line

At this moment, the correct statement is:

- the backend architecture is isolated enough for a new frontend
- the backend **project targeting is not yet ready** for direct WinUI 3 consumption

That gap must be handled deliberately before the production WinUI host is created.
