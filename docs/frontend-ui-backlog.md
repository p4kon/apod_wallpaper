# Frontend UI Backlog

This document captures product-facing UI requirements that belong to the future frontend build, not to the WinUI 3 proof-of-concept.

## Product Direction

The application should stay minimal and focused:

- simple wallpaper-first experience
- low-friction settings
- clear visual states
- desktop behavior that feels calm and predictable

## Settings Model

### Autosave

All settings should persist immediately.

- no `Save`
- no `Cancel`
- changes apply on the fly where technically safe

This is already aligned with the current backend behavior and should remain the rule in the new frontend.

## Main Window Behavior

### Startup

- app starts with the main window open
- it should not start hidden in tray by default

### Close Button

The `X` button should be user-configurable:

- `Close application`
- `Minimize to tray`

This should be exposed as a regular setting in the new frontend.

## Calendar UX

The calendar should support visual date-state highlighting.

### Color States

#### Green

Date has a locally downloaded image file.

Meaning:

- the image is already on disk
- backend can likely load preview locally

#### Blue

NASA published an image for that date, but the image is not downloaded locally.

Meaning:

- APOD is available as image content
- user can still choose to download/apply it

#### Red

NASA published non-image content for that date.

Examples:

- video
- media format that cannot be applied as wallpaper

### Calendar Data Policy

Rich calendar coloring should be treated as a **personal API key feature**.

Reason:

- `DEMO_KEY` has limited request volume
- per-date availability coloring across a calendar is too expensive for demo-key usage

Expected rule:

- with personal API key: enable full calendar enrichment
- with `DEMO_KEY` or no key: use reduced or cached-only calendar intelligence

## API Key UX

The NASA API key should move out of the main dense settings area and become a dedicated interaction.

### Desired Flow

- main window contains a dedicated API key status button
- pressing it opens a pop-up / dialog / dedicated settings surface

### API Key Surface Content

The popup should contain:

1. Short explanation of why a personal API key is useful
2. Link button to the NASA API key page
3. Text field to paste the key
4. Small note that if the NASA page is inaccessible, the user may need VPN

### API Key Status Indicator

The main UI should show lightweight status color around the API key entry point:

- light green = valid personal key
- light red = invalid key
- pale blue = no key or `DEMO_KEY`

This should be subtle, not alarmist.

## Main Content Layout

The current app is functionally dense. The new frontend should separate concerns more clearly:

1. Preview area
2. Date selection
3. Main actions
4. Settings/accessory actions

The UI should remain compact, but it should no longer feel like all controls are packed into one legacy dialog.

## Frontend Build Order

The production frontend should **not** start with a full multi-page shell.

Reason:

- this product is still primarily a single-flow wallpaper tool
- building `NavigationView` / `Frame` / page shell too early would add WinUI boilerplate before the real interaction model is proven
- preview/apply behavior should shape the shell, not the other way around

### Required Order

1. Build a single-page working preview surface first
2. Connect date selection, preview loading, apply/download actions
3. Validate what truly needs separate navigation
4. Only then introduce a larger shell if the UI actually needs multiple surfaces

### Shell Rule

Until preview/apply/settings flows are proven in the real host:

- prefer one working page
- avoid committing early to `NavigationView`
- avoid adding XAML shell structure just for architecture aesthetics

## Notes For Implementation

- The calendar highlighting depends on backend month/date state and should not be hardcoded in UI.
- The backend should remain the source of truth for:
  - local image existence
  - APOD availability state
  - image vs non-image classification
- The frontend should render these states, not infer them.

## Known Follow-Ups

- Startup still appears to trigger two overlapping paths for the first "today" preview in some sessions: the preview workflow and month-state initialization can both probe the same date at nearly the same time. This should be reduced carefully later without regressing month coloring or startup responsiveness.

## State Screen vs Settings Screen

To avoid duplicate screens with the same data, the frontend should treat startup state and settings as the **same control surface**.

### Task 5 Scope

Task 5 should build the first **readonly control screen** from `GetInitialStateAsync()`:

- selected date
- wallpaper style
- images directory
- auto-check state
- startup state
- API key state

This screen exists to prove:

- one-call startup rendering
- clean loading model
- stable state presentation

### Task 8 Scope

Task 8 should not invent a second screen with the same data.

Instead, Task 8 should evolve the same control surface from readonly state cards into live editable controls where appropriate.

That means:

- Task 5 = render state clearly
- Task 8 = make the same settings blocks interactive
