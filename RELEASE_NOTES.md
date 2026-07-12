# APOD Wallpaper 1.2.0

Localization and upgrade reliability build.

## Download

- `APODWallpaper-1.2.0.0-win-x64-setup.exe` - recommended installer for normal testing. It bundles Windows App Runtime 2.0.1.
- `APODWallpaper-1.2.0.0-win-x64-self-contained-portable.zip` - portable build. Extract the whole zip first, then run `APODWallpaper.exe`.

## Highlights

- Compact WinUI calendar with Local / Available / Video / Unchecked states.
- Preview panel with NASA APOD image and explanation.
- Download, Apply, Auto On, and NASA page actions.
- Smart wallpaper fitting for wide, tall, square, and unusual images.
- Tray-friendly behavior, startup setting, and local settings storage.
- English / Russian / System language selection with persisted preference.
- About page version detection for portable and installer builds.
- Installer update flow targets only the APOD Wallpaper process when closing a running app.
- Portable layout keeps app binaries in `app`, data in `data`, and images in `images`.
- Setup installer installs the required Windows App Runtime for clean PCs.

## Notes

- Windows 10/11 x64.
- Setup installer is the safest choice for clean PCs.
- Portable build is self-contained for .NET, but WinUI 3 still requires Windows App Runtime on the machine.
- The build is unsigned; Windows SmartScreen may show a warning until a trusted signing certificate is available.
