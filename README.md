# APOD Wallpaper

APOD Wallpaper is a small Windows desktop app for browsing NASA's Astronomy Picture of the Day and applying available images as your wallpaper.

The app is built around a compact calendar view: available image days, video/unsupported days, and locally saved days are visible at a glance. It can also run quietly in the system tray and automatically apply the latest available APOD image.

Website: https://apod_wallpaper.p4kon.com

## Features

- Browse APOD entries by date.
- Preview APOD images and explanation text.
- Copy APOD explanation text.
- Open APOD explanation text in Google Translate with a selectable target language.
- Download images locally.
- Apply wallpapers using Smart, Fill, Fit, Stretch, Center, Tile, or Span modes.
- Automatically check for the latest APOD image.
- Skip video/unsupported APOD days.
- Run from the system tray.
- Store an optional NASA API key locally.
- Switch the UI between English and Russian.

## Requirements

- Windows 10 version 2004 or newer, or Windows 11.
- x64 Windows.
- The setup installer bundles Windows App Runtime 2.0.1.
- The portable build includes the .NET desktop runtime files used by the app, but still needs Windows App Runtime on the machine.

## Status

APOD Wallpaper is currently in active testing. Public builds are distributed through GitHub Releases:

- Portable zip: extract the whole folder and run `APODWallpaper.exe`.
- Setup exe: installs the app for the current Windows user and creates a Start menu shortcut.

MSIX packaging is kept as a technical path, but it is not the primary public distribution channel right now.

## Build a Release

```powershell
.\scripts\publish-portable.ps1 -Clean
```

By default the release script creates the public portable package and setup installer. The portable package includes the .NET desktop runtime files used by the app, while the setup installer also bundles Windows App Runtime 2.0.1 for clean machines.

For offline testing only, you can create a larger self-contained build:

```powershell
.\scripts\publish-portable.ps1 -Clean -SelfContained
```

The release script creates:

- `artifacts\release\APODWallpaper-<version>-win-x64-portable.zip`
- `artifacts\release\APODWallpaper-<version>-win-x64-portable.zip.sha256`
- `artifacts\release\setup\APODWallpaper-<version>-win-x64-setup.exe`, if Inno Setup 6 is installed.

The portable build includes `portable.mode` and pre-created `images`, `images\smart`, and `data` folders. At runtime the app resolves these paths from the folder containing the executable, so every user can place the portable folder wherever they want.

The binaries are not code-signed yet. Windows SmartScreen may show a warning until a trusted signing certificate is available.

## NASA APOD content

APOD Wallpaper is an independent app and is not affiliated with, endorsed by, or sponsored by NASA.

APOD images, videos, explanations, and credits are provided by NASA APOD and may be owned by NASA or third-party copyright holders. The app displays and downloads publicly available APOD content for the user's local wallpaper workflow.

See [NOTICE.md](NOTICE.md) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for more details.

## Privacy

See the public [privacy policy](https://apod_wallpaper.p4kon.com/privacy.html) or the repository copy in [PRIVACY.md](PRIVACY.md).

## Support

For bugs and feature requests, use [GitHub Issues](https://github.com/p4kon/apod_wallpaper/issues).

For direct support, email <p4kon1@gmail.com>.

## License

This project is licensed under the [MIT License](LICENSE).
