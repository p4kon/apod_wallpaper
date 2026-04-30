# Third-Party Notices

Last audited: April 30, 2026

## Audit result

This repository does not use any third-party NuGet packages or `packages.config`-based dependencies.

All three projects in the solution are built only on:

- project-to-project references inside this repository
- Microsoft .NET Framework assemblies
- Windows desktop platform APIs that ship with the operating system or Visual Studio/.NET Framework developer tooling

No GPL, LGPL, AGPL, or other copyleft package dependencies were found.

## Project inventory

| Project | PackageReference | packages.config | External NuGet packages |
|---|---|---|---|
| `apod_wallpaper` | none | none | none |
| `apod_wallpaper.Core` | none | none | none |
| `apod_wallpaper.SmokeTests` | none | none | none |

## Referenced framework assemblies

These are framework/platform references declared in the project files. They are not vendored in this repository as third-party package source code.

| Assembly | Referenced by |
|---|---|
| `Microsoft.CSharp` | `apod_wallpaper`, `apod_wallpaper.Core`, `apod_wallpaper.SmokeTests` |
| `System` | `apod_wallpaper`, `apod_wallpaper.Core`, `apod_wallpaper.SmokeTests` |
| `System.Core` | `apod_wallpaper`, `apod_wallpaper.Core`, `apod_wallpaper.SmokeTests` |
| `System.Data` | `apod_wallpaper` |
| `System.Data.DataSetExtensions` | `apod_wallpaper` |
| `System.Deployment` | `apod_wallpaper` |
| `System.Drawing` | `apod_wallpaper`, `apod_wallpaper.Core`, `apod_wallpaper.SmokeTests` |
| `System.Net.Http` | `apod_wallpaper`, `apod_wallpaper.Core` |
| `System.Runtime.Serialization` | `apod_wallpaper`, `apod_wallpaper.Core` |
| `System.Security` | `apod_wallpaper.Core` |
| `System.Windows.Forms` | `apod_wallpaper`, `apod_wallpaper.Core`, `apod_wallpaper.SmokeTests` |
| `System.Xml` | `apod_wallpaper`, `apod_wallpaper.Core` |
| `System.Xml.Linq` | `apod_wallpaper`, `apod_wallpaper.Core` |

## Transitive dependency status

Because there are no NuGet packages in the solution, there are no transitive package dependencies to audit for license inheritance or copyleft obligations.

The only transitive runtime surface comes from:

- the .NET Framework runtime
- Windows OS APIs such as Registry, DPAPI, WinForms, and `user32.dll`

These are platform dependencies, not third-party redistributable packages within this repository.

## Bootstrapper packages

The WinForms host project declares Visual Studio bootstrapper metadata for:

- `.NETFramework,Version=v4.8`
- `Microsoft.Net.Framework.3.5.SP1`

These entries are installer/bootstrapper metadata only. They are not third-party source dependencies included in this repository and do not introduce copyleft obligations to this codebase.

## External services

This application integrates with NASA services at runtime:

- [NASA APOD API](https://api.nasa.gov/)
- [NASA APOD](https://apod.nasa.gov/apod/)

These are service integrations, not code dependencies bundled with the repository. Runtime use is governed by NASA's own service terms, rate limits, and availability.

## Conclusion

The dependency surface audited on April 30, 2026 is compatible with the repository's MIT license:

- no third-party NuGet packages are present
- no copyleft package dependencies were found
- no untracked package license obligations were identified in the current solution structure
