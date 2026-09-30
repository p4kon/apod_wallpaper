# APOD Wallpaper 2.0.0

This release updates APOD Wallpaper for NASA's new APOD website, restoring date-based browsing, image downloads, and wallpaper updates with the new source.

## What's improved

### Support for the new NASA APOD website

- Calendar browsing and Random APOD now use the new NASA Science source.
- Current publications and the historical archive remain accessible by date.
- A NASA API key is no longer required for browsing or downloading APOD images.
- The NASA button opens the page for the selected publication on the new website.

### Images and downloads

- Previews use smaller image versions when available, while downloads use the original image.
- Improved handling of video, text-only publications, and supported static image frames.
- Video posters are not treated as downloadable APOD images.
- Improved recovery from unavailable previews and interrupted downloads.
- Existing downloaded images and Favorites remain available locally.

### Reliability and usability

- Reduced repeated requests when checking the latest APOD publication fails.
- Improved reuse of publication information between the calendar, Random APOD, and previews.
- Favorites are saved only after a valid local image is available.
- Manual wallpaper actions disable Auto only after a successful apply and use up-to-date settings.
- Added missing Russian translations for download, wallpaper, and NASA page errors.

## Downloads

- `APODWallpaper-2.0.0.0-win-x64-setup.exe`
  Recommended for most users. Installs APOD Wallpaper for the current Windows user and supports upgrading over previous versions.

- `APODWallpaper-2.0.0.0-win-x64-portable.zip`
  Portable version. Extract the whole archive first, then run `APODWallpaper.exe`.

## Notes

- Windows 10 version 2004 or newer, or Windows 11, x64.
- Setup installer is recommended for clean installations and upgrades.
- Portable builds keep app binaries, downloaded images, cache, logs, and settings inside the extracted folder.
- Previously downloaded files are preserved. This update does not automatically replace incorrect images, such as a NASA logo, saved by older versions during the website transition.
- The build is not code-signed yet, so Windows SmartScreen may show a warning.
- APOD Wallpaper is an independent project and is not affiliated with, endorsed by, or sponsored by NASA.

# APOD Wallpaper 1.3.0

This release adds a much more complete APOD browsing and wallpaper workflow: favorites, random discovery, year overview, local library summary, update checks, favorite-based wallpaper rotation, download progress, and several calendar/preview stability improvements.

## What's new

### Favorites

- Added a new Favorites page for saved local APOD images.
- Favorite dates are marked in the calendar.
- Favorite images are shown as thumbnail tiles.
- Favorite preview now opens in a fullscreen-style viewer with a blurred, darkened backdrop.
- The preview can open the selected APOD date back in the calendar.
- Favorite add/remove behavior is local-only and does not require an account.

### Random APOD

- Added Random APOD picker.
- Supported sources:
  - Global APOD archive
  - Downloaded images
  - Favorites
- Added optional deep archive mode for older APOD dates.
- Random APOD opens the selected date for preview and does not automatically download or apply wallpaper.

### Year view

- Added a cache-first calendar year view.
- Users can quickly scan APOD availability across the whole year.
- Clicking a day from the year view opens that date in the normal calendar flow.
- Year view does not spam NASA requests and does not trigger downloads or wallpaper changes.

### Local library summary

- Added a Library page with local storage information.
- Shows downloaded images, smart variants, cache, logs, and app data folders.
- Added quick actions to open images and data folders.
- This release does not add cleanup/delete actions.

### Favorite wallpaper rotation

- Automatic wallpaper mode can now use downloaded favorites as the wallpaper source.
- The existing latest-APOD automation remains available.
- Favorite rotation avoids immediately repeating the same favorite when alternatives exist.

### Update checker

- Added manual update check in the About page.
- Added optional automatic update checks.
- The app checks GitHub Releases and can open the latest release page.
- Updates are not downloaded or installed automatically.

### Download progress

- Image download and apply flows now show progress.
- Progress can include downloaded size and speed when available.
- Applies to normal download/apply actions and favorite download flows.

## Improvements and fixes

- Improved calendar month navigation and rendering stability.
- Reduced stale async calendar updates during fast navigation.
- Improved year view layout and scrolling behavior.
- Fixed downloaded image date scanning so smart wallpaper artifacts are not treated as original downloaded APOD dates.
- Improved handling of text-only APOD pages by treating them as unsupported media instead of leaving them as unchecked.
- Improved fullscreen favorite preview:
  - fixed first-click behavior;
  - improved backdrop rendering;
  - removed broken transparency approach;
  - reduced border/chrome artifacts.

## Privacy and network notes

- APOD Wallpaper remains local-first.
- Favorites, thumbnails, settings, cache, downloaded images, and logs are stored locally.
- The optional NASA API key remains local.
- The update checker contacts GitHub Releases to check the latest available version.
- Google Translate opens in the default browser only when the user chooses the Translate action.
- No analytics, telemetry, ads, accounts, or in-app purchases were added.

## Downloads

- `APODWallpaper-1.3.0.0-win-x64-setup.exe`
  Recommended for most users. Installs APOD Wallpaper for the current Windows user and supports upgrading over previous versions.

- `APODWallpaper-1.3.0.0-win-x64-portable.zip`
  Portable version. Extract the whole archive first, then run `APODWallpaper.exe`.

- `APODWallpaper-1.3.0.0-win-x64-portable.zip.sha256`
  SHA256 checksum for the portable zip.

## Notes

- Windows 10/11 x64.
- Setup installer is recommended for clean installations and upgrades.
- Portable builds keep app binaries, downloaded images, cache, logs, and settings inside the extracted folder.
- The build is not code-signed yet, so Windows SmartScreen may show a warning.
- APOD Wallpaper is an independent project and is not affiliated with, endorsed by, or sponsored by NASA.

# APOD Wallpaper 1.2.1

This release adds a responsive APOD availability check for the calendar.

## What's changed

- Added a quick read-only check for today's NASA APOD HTML page when Calendar/MainPage is opened, restored, or reactivated.
- If today's APOD page is already published, the calendar can unlock today as `unchecked` without waiting for the normal background refresh path.
- The probe does not download images, apply wallpaper, save settings, or run scheduler/apply/download logic.
- Scheduler and wallpaper automation behavior is unchanged.
- Updated application version to `1.2.1.0`.

## Downloads

- `APODWallpaper-1.2.1.0-win-x64-setup.exe`
  Recommended for most users. Installs APOD Wallpaper, creates shortcuts, bundles Windows App Runtime 2.0.1, and supports upgrading over previous installations.

- `APODWallpaper-1.2.1.0-win-x64-portable.zip`
  Portable version. Extract the whole archive first, then run `APODWallpaper.exe`.

- `APODWallpaper-1.2.1.0-win-x64-portable.zip.sha256`
  SHA256 checksum for the portable zip.

## Notes

- Windows 10/11 x64.
- Setup installer is the safest choice for clean PCs and upgrades.
- Portable builds keep app binaries in `app`, user data in `data`, and downloaded images in `images`.
- Portable builds include the .NET desktop runtime files needed by the app, but WinUI 3 still requires Windows App Runtime on the target machine.
- The build is unsigned; Windows SmartScreen may show a warning until a trusted signing certificate is available.
- APOD Wallpaper is an independent app and is not affiliated with, endorsed by, or sponsored by NASA.

# APOD Wallpaper 1.2.0

This release adds full English / Russian localization, improves the APOD explanation workflow, and makes installer upgrades safer when APOD Wallpaper is already running.

## What's changed

- Added deterministic English / Russian UI localization.
- Added a language selector in Settings.
- Localized calendar month names, weekdays, legend labels, statuses, settings text, About text, tray menu text, and user-visible fallback messages.
- Added Copy and Translate actions for the APOD explanation text.
- Added a compact translation target selector for `ru`, `es`, `de`, `fr`, `it`, `pt`, and `ja`.
- Google Translate opens with the selected target language, while Copy keeps using the displayed explanation text.
- Improved long-text translation fallback: the app copies the explanation to the clipboard and opens Google Translate only when copying succeeds.
- Fixed About version detection for portable and installer builds.
- Improved installer upgrade behavior so a running `apod_wallpaper.WinUI.exe` is closed before files are replaced.
- Kept the installer AppId, AppName, and default install directory stable for upgrades from 1.1.1.
- Updated application version to `1.2.0.0`.

## Downloads

- `APODWallpaper-1.2.0.0-win-x64-setup.exe`
  Recommended for most users. Installs APOD Wallpaper, creates shortcuts, bundles Windows App Runtime 2.0.1, and supports upgrading over the previous 1.1.1 installation.

- `APODWallpaper-1.2.0.0-win-x64-portable.zip`
  Portable version. Extract the whole archive first, then run `APODWallpaper.exe`.

- `APODWallpaper-1.2.0.0-win-x64-portable.zip.sha256`
  SHA256 checksum for the portable zip.

## Notes

- Windows 10/11 x64.
- Setup installer is the safest choice for clean PCs and upgrades.
- Portable builds keep app binaries in `app`, user data in `data`, and downloaded images in `images`.
- Portable builds include the .NET desktop runtime files needed by the app, but WinUI 3 still requires Windows App Runtime on the target machine.
- The build is unsigned; Windows SmartScreen may show a warning until a trusted signing certificate is available.
- APOD Wallpaper is an independent app and is not affiliated with, endorsed by, or sponsored by NASA.

## Checksums

```text
D63A008BA78485CED478EEB34CB9D54D32987B406C1D90070DADCA0A35727451  APODWallpaper-1.2.0.0-win-x64-portable.zip
```
