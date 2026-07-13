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
