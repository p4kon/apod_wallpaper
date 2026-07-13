# APOD Wallpaper release checklist

Use this checklist before publishing a public GitHub release.

## Build

- Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-portable.ps1 -Clean`.
- Confirm `artifacts\release\APODWallpaper-<version>-win-x64-portable.zip` exists.
- Confirm `artifacts\release\APODWallpaper-<version>-win-x64-portable.zip.sha256` exists.
- Confirm `artifacts\release\setup\APODWallpaper-<version>-win-x64-setup.exe` exists.
- Confirm the setup installer bundles Windows App Runtime under `artifacts\release\prerequisites`.
- Confirm the portable folder contains `portable.mode`, `images`, `images\smart`, `data`, `data\cache`, `data\logs`, and `data\secrets`.
- Confirm the portable folder is not accidentally single-file/self-contained. The main executable should be small, not hundreds of MB.
- Use `.\scripts\publish-portable.ps1 -Clean -SelfContained` only when intentionally producing a larger offline build.

## Smoke Test

- Portable: unzip to a clean folder and launch `APODWallpaper.exe`.
- Installer: install from `setup.exe`, launch after install, then uninstall from Windows Apps.
- On a clean machine, prefer the setup installer first because it installs Windows App Runtime.
- Portable may still require Windows App Runtime if it is not already installed.
- Verify preview loading, Download, Apply, Auto On, tray hide/show, and settings persistence.
- Verify English / Russian UI language switching and cold start persistence.
- Verify APOD explanation Copy and Translate actions.
- Verify translation target selector displays `ru`, `es`, `de`, `fr`, `it`, `pt`, or `ja` after selection.
- Verify About shows version `1.2.0` or `1.2.0.0`.
- Verify the default download folder is created next to the portable executable for portable builds.
- Verify startup behavior only after installing or moving the app to its final path.
- Verify installing 1.2.0 over 1.1.1 updates the existing installation rather than creating a second installed app entry.

## GitHub Release

- Create or update a tag, for example `v1.2.0`.
- Upload the portable `.zip` and setup `.exe`.
- Upload the portable `.zip.sha256` checksum.
- Mention that the build is currently unsigned, so Windows SmartScreen may show a warning.
- Link the website: `https://apod_wallpaper.p4kon.com`.

## Website

- Confirm the Download button points to `https://github.com/p4kon/apod_wallpaper/releases/latest`.
- Redeploy `website/` after changing screenshots, copy, or download links.
