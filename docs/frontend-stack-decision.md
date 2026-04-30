# Frontend Stack Decision

## Status

Accepted as the working implementation path for the new frontend.

## Decision

The new frontend will target **WinUI 3 (Packaged desktop app)** on Windows.

- New host project name target: `apod_wallpaper.WinUI`
- `apod_wallpaper.Core` remains the backend library
- The new frontend is a thin host over the existing backend contracts
- No new frontend code should depend on WinForms-specific APIs

This is a **gated decision**, not an unconditional build-out:

1. WinUI 3 is the selected primary path.
2. Before building the full production frontend, we must pass a short proof-of-concept for:
   - tray integration
   - wallpaper apply in a packaged app context
3. If the proof-of-concept fails for reasons that materially break the product, we stop the WinUI track early and switch host strategy before investing in the main UI.

## Why WinUI 3

WinUI 3 is currently the best fit for the product goals:

- Windows-first desktop application
- modern UI stack for future Microsoft Store publishing
- packaged app model aligns with MSIX distribution
- backend is already isolated enough to be hosted by a new UI stack

## Mandatory Gate Before Main Frontend Work

### Proof-of-Concept

Before creating the full `apod_wallpaper.WinUI` production frontend, create a short-lived packaged WinUI 3 proof-of-concept that validates the two riskiest product assumptions:

1. **Tray behavior**
2. **Wallpaper apply from packaged host**

### Proof-of-Concept Success Criteria

The PoC is considered successful only if all conditions below are met:

1. A packaged WinUI 3 app launches as a normal top-level desktop window.
2. The app can minimize to tray after the window is closed or hidden.
3. The app can restore the main window from tray interaction.
4. The app can call the backend and successfully apply a wallpaper from a packaged context.
5. No Windows/App SDK restriction is discovered that would force a product redesign around tray or wallpaper application.

### Proof-of-Concept Failure Criteria

The PoC is considered failed if one or more of the following are true:

1. Tray behavior is impossible or unstable in the packaged WinUI path.
2. Wallpaper application cannot work reliably from the packaged app model.
3. The required workaround would introduce a second hidden process or architecture complex enough to erase the benefit of moving to WinUI 3.

If the PoC fails, the WinUI track should be paused before building the full UI, and the fallback host decision should be revisited.

## Fallback Host If PoC Fails

If the WinUI 3 packaged PoC does not pass, the fallback host should be **WPF** as the next Windows-native candidate.

WPF is not the primary choice, but it is the safety option because:

- tray behavior is straightforward
- classic desktop integration is simpler
- backend isolation work remains reusable

## Host Responsibilities

The new frontend host will be responsible for:

- application bootstrap
- window lifecycle
- tray behavior
- view models and UI state binding
- user interaction
- host-specific startup integration

The new frontend host must **not** reimplement:

- NASA API access
- HTML fallback logic
- calendar availability logic
- image download/apply workflow
- settings semantics
- secure secret handling
- smart wallpaper composition rules

Those remain in `apod_wallpaper.Core`.

## Explicit Tray UX Decision

The new frontend should **not** start hidden in tray by default.

Default startup behavior:

1. Launch the main application window.
2. When the user clicks `X`, obey a user setting:
   - `Close application`
   - `Minimize to tray`

This behavior is required because it is clearer for new users and safer for future Store-facing UX.

## Planned Host Composition

The future WinUI host should compose the backend using the public contracts already prepared in `apod_wallpaper.Core`:

- `JsonSettingsStore`
- `DpapiUserSecretStore`
- `IStartupRegistrationService` implementation for the new host
- `ApplicationController`

## Implementation Order

The first frontend phase should proceed in this order:

1. Create PoC packaged WinUI 3 app.
2. Validate tray behavior and wallpaper apply.
3. Confirm host viability.
4. Create the real `apod_wallpaper.WinUI` project.
5. Build the production frontend on top of `apod_wallpaper.Core`.

## Notes

- The current local CLI template list does not expose a WinUI 3 template directly, so project scaffolding should be done through Visual Studio's WinUI packaged app template when this phase starts.
- This does not block the decision; it only affects how the host project is created.
