# WinUI 3 Packaged PoC

## Goal

Validate the riskiest host assumptions before creating the real `apod_wallpaper.WinUI` frontend project.

The proof-of-concept exists to answer one question:

> Can a packaged WinUI 3 desktop host support the tray and wallpaper workflow without forcing a major product redesign?

## Scope

This PoC is **not** the new production frontend.

It is intentionally small and disposable.

The PoC should validate only the host/platform risk areas:

1. Packaged WinUI 3 app bootstrap
2. Tray integration behavior
3. Main window show/hide lifecycle
4. Calling `apod_wallpaper.Core` from the new host
5. Wallpaper apply from the packaged context

## Explicit Non-Goals

The PoC should **not** attempt to build:

- the final visual design
- the final settings screen
- calendar date coloring
- API key editor UX
- final app navigation
- full Store metadata

Those belong to the real frontend implementation after the PoC passes.

## Required Host Behavior

The PoC must prove the following behavior works:

1. The app launches as a regular top-level window.
2. The user can close the window into tray instead of terminating the process.
3. The tray interaction can restore the main window.
4. The tray interaction can trigger at least one backend action.
5. The host can call backend initialization and at least one wallpaper workflow operation successfully.

## Minimal Backend Integration

The PoC must use the real backend composition shape:

- `JsonSettingsStore`
- `DpapiUserSecretStore`
- host `IStartupRegistrationService`
- `ApplicationController`

It is acceptable for the PoC startup registration service to be a temporary no-op implementation if startup integration is not part of the spike.

## Required Scenarios

The PoC passes only if all scenarios below work:

### Scenario 1: App startup

- Launch packaged WinUI 3 app
- Window appears normally
- Backend initializes successfully

### Scenario 2: Close-to-tray

- User presses `X`
- Window hides instead of terminating
- Process remains alive
- Tray icon remains available

### Scenario 3: Restore from tray

- User activates tray icon
- Main window returns
- UI remains responsive

### Scenario 4: Backend call from host

- Host requests initial backend state
- Host receives valid snapshot
- Host can display a simple diagnostic result on screen

### Scenario 5: Wallpaper apply

- Host triggers a backend wallpaper apply flow
- Wallpaper changes successfully from packaged host context

## Success Criteria

The PoC is successful if:

1. Tray lifecycle is stable.
2. Packaged host can call backend without architectural hacks.
3. Wallpaper apply works.
4. No blocker is found that would force a redesign of the app's Windows behavior.

## Failure Criteria

The PoC is considered failed if any of the following happen:

1. Tray support in packaged WinUI 3 is unstable or effectively unusable.
2. Wallpaper application fails due to packaged host restrictions.
3. The only workaround requires a second hidden helper process or architectural split that materially complicates the product.
4. The host must depend on WinForms in order to function.

## Deliverables

The PoC phase should end with:

1. A tiny packaged WinUI 3 spike project
2. A short written result:
   - pass or fail
   - what worked
   - what did not work
   - what host changes are required for the real project

## Decision After PoC

### If PoC passes

- Create the real `apod_wallpaper.WinUI` frontend project
- Start implementation of the production UI

### If PoC fails

- Stop WinUI 3 track early
- Revisit the host decision
- Evaluate WPF as the fallback Windows host
