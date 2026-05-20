# Contributing

Thanks for your interest in APOD Wallpaper.

The project is currently moving toward a stable WinUI/MSIX release. Small, focused issues and pull requests are easiest to review.

## Development setup

Recommended environment:

- Windows 10 or Windows 11
- Visual Studio with .NET desktop development and Windows App SDK tooling
- .NET 8 SDK

## Build

```powershell
dotnet build .\apod_wallpaper.WinUI\apod_wallpaper.WinUI.csproj -c Debug -p:Platform=x64
```

## Test

```powershell
dotnet build .\apod_wallpaper.SmokeTests\apod_wallpaper.SmokeTests.csproj -c Debug
```

The smoke-test project runs its test executable after build by default.

## Pull requests

- Keep changes focused.
- Avoid mixing UI redesign, backend behavior changes, and packaging changes in one pull request.
- Do not commit local build outputs from `artifacts`, `bin`, or `obj`.
- Do not include personal NASA API keys, local paths, or private logs.

## NASA APOD content

Do not add NASA/APOD images to the repository unless their usage and credits are clearly documented.
