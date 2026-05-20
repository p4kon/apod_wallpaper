# Third-Party Notices

Last audited: May 20, 2026

## Summary

APOD Wallpaper is licensed under the MIT License.

The current app is built from:

- project source code in this repository
- Microsoft .NET / Windows platform components
- NuGet packages listed below
- NASA APOD services used at runtime

No GPL, LGPL, AGPL, or other copyleft package dependencies were identified in the current project files during this audit.

## NuGet packages

Top-level package references:

| Project | Package | Version |
|---|---|---|
| `apod_wallpaper.WinUI` | `Microsoft.Windows.SDK.BuildTools` | `10.0.28000.1839` |
| `apod_wallpaper.WinUI` | `Microsoft.WindowsAppSDK` | `2.0.1` |
| `apod_wallpaper.Core` | `System.Drawing.Common` | `8.0.0` |
| `apod_wallpaper.Core` | `System.Security.Cryptography.ProtectedData` | `8.0.0` |

These packages are Microsoft/.NET platform packages used to build and run the Windows desktop application.

## Project inventory

| Project | Purpose |
|---|---|
| `apod_wallpaper.Core` | Backend/domain logic, APOD workflow, settings, storage, wallpaper operations |
| `apod_wallpaper.WinUI` | WinUI desktop frontend and MSIX/portable host |
| `apod_wallpaper.SmokeTests` | Local smoke-test executable |

## External services

This application integrates with NASA services at runtime:

- [NASA APOD](https://apod.nasa.gov/apod/)
- [NASA APOD API](https://api.nasa.gov/)

These are service integrations, not code dependencies bundled with this repository. Runtime use is governed by NASA's own service terms, rate limits, availability, and content credits.

## APOD content

APOD images, videos, explanations, titles, and credits may be owned by NASA or third-party copyright holders credited on each APOD page.

APOD Wallpaper does not claim ownership of APOD content. The app displays and downloads publicly available APOD content for the user's local wallpaper workflow.

## Removed legacy projects

The legacy WinForms host and the temporary .NET 8 probe project were removed after the WinUI/MSIX path passed local smoke testing.

## Conclusion

As of this audit, the dependency surface is suitable for continuing toward a public Windows desktop release, with final Store submission still requiring review of Microsoft Store policy, NASA content attribution, and public privacy/support URLs.
