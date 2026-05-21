# Microsoft Store metadata draft

Date: 2026-05-20

This document is a draft for the Microsoft Store listing. It should be reviewed before submission, especially URLs, screenshots, and support contact details.

Microsoft references:

- Store submission FAQ: https://learn.microsoft.com/windows/apps/publish/faq/submit-your-app
- Screenshots and images: https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/screenshots-and-images
- Age ratings: https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/age-ratings
- Store policy metadata accuracy: https://learn.microsoft.com/windows/apps/publish/store-policy-archive/store-policy-7-16-1

## App identity

App name:

```text
APOD Wallpaper
```

Short name / title bar:

```text
APOD Wallpaper
```

Publisher display name:

```text
p4kon
```

## Category

Recommended category:

```text
Personalization
```

Alternative category if Personalization is unavailable in Partner Center:

```text
Utilities & tools
```

Reasoning:

APOD Wallpaper changes the desktop wallpaper and is primarily a desktop personalization app. It also has utility behavior, but "Personalization" is the more accurate user-facing category.

## Short description

```text
Preview, download, and apply NASA Astronomy Picture of the Day images as your Windows wallpaper.
```

## Store description

```text
APOD Wallpaper is a small Windows desktop app for browsing NASA's Astronomy Picture of the Day and applying available images as your wallpaper.

Open the app to see a calendar of APOD entries, preview available images, download them locally, and apply them to your desktop. The app can also check for new APOD images automatically and keep your wallpaper updated when a new image is available.

Key features:

- Calendar view for browsing APOD dates
- Image preview with APOD explanation text
- Download and apply wallpaper actions
- Automatic daily wallpaper checks
- Wallpaper fit modes: Smart, Fill, Fit, Stretch, Center, Tile, and Span
- Tray support for running quietly in the background
- Optional NASA API key support

APOD Wallpaper is an independent app and is not affiliated with, endorsed by, or sponsored by NASA. APOD images and videos may be owned by NASA or third-party copyright holders credited on each APOD page.
```

## Search keywords

Primary keywords:

```text
APOD
NASA
wallpaper
astronomy
space
desktop wallpaper
picture of the day
astronomy picture of the day
cosmos
background
```

Secondary keywords:

```text
space wallpaper
NASA APOD
daily wallpaper
science
universe
nebula
galaxy
stars
desktop background
```

## Feature bullets

```text
Browse NASA APOD images by date.
Preview available images before applying them.
Download APOD images locally.
Apply images using multiple wallpaper styles.
Automatically check for new daily APOD images.
Run quietly from the system tray.
Use an optional NASA API key.
```

## Screenshots

Microsoft requires at least one screenshot for submission and supports up to 10 desktop screenshots. Recommended set for this app:

1. Main calendar screen with colored date states and preview visible.
2. Main screen with an APOD image preview and explanation text.
3. Settings screen showing wallpaper style, auto-check, startup, and download folder.
4. API key configuration dialog.
5. About screen.
6. Tray context menu if it can be captured cleanly.

Screenshot guidance:

- Use clean data and avoid personal paths if possible.
- Prefer Windows 11 screenshots first.
- Also keep at least one Windows 10 screenshot for internal validation, even if not submitted.
- Avoid showing Visual Studio, logs, or debug overlays.
- Use real APOD content only if credits/rights are acceptable for Store marketing assets.

Open question:

```text
TODO: decide whether Store screenshots should use real NASA/APOD imagery or neutral placeholder/sample imagery.
```

## Age rating

Expected rating direction:

```text
Suitable for general audiences, likely low age rating after IARC questionnaire.
```

Important notes:

- Microsoft Store uses the IARC questionnaire for age ratings.
- The final rating is assigned by the questionnaire, not manually selected in this document.
- The app downloads and displays NASA APOD content, which can occasionally include astronomy images, videos, or external links.
- The app does not include user-generated content, chat, purchases, gambling, explicit content, or social sharing.
- The app opens NASA/APOD pages in the user's browser.

Suggested IARC questionnaire answers should reflect:

- No violence created by the app.
- No sexual content created by the app.
- No gambling.
- No in-app purchases.
- No user-generated content.
- Internet access is used to retrieve APOD metadata/media and open APOD pages.

## Support info

Official website:

```text
https://apod_wallpaper.p4kon.com
```

Support contact:

```text
GitHub Issues: https://github.com/p4kon/apod_wallpaper/issues
Email: p4kon1@gmail.com
```

Recommended support URL:

```text
https://apod_wallpaper.p4kon.com/#support
```

Support text:

```text
For bug reports, feature requests, or help with APOD Wallpaper, please use the GitHub Issues page or email p4kon1@gmail.com.
```

## Privacy policy URL

Required before Store submission:

```text
https://apod_wallpaper.p4kon.com/#privacy
```

Recommended options:

1. Use the public website privacy section for Microsoft Store submission.
2. Keep `PRIVACY.md` in the public GitHub repository as the source-of-truth long-form policy.

Privacy summary draft:

```text
APOD Wallpaper stores settings locally on the user's device. If the user enters a NASA API key, it is stored locally for use with NASA APOD requests. The app contacts NASA APOD services to retrieve metadata and images. The app does not sell personal data and does not include analytics or advertising.
```

## Data and network disclosure

The Store listing / privacy policy should clearly mention:

- The app connects to NASA APOD services.
- The app may download images to the selected local folder.
- The app can change the user's desktop wallpaper.
- The app can run at Windows startup if the user enables that setting.
- The app can keep running in the system tray.
- Optional NASA API key is used only for NASA APOD requests and stored locally.

## NASA / APOD disclaimer

Recommended listing disclaimer:

```text
APOD Wallpaper is an independent application and is not affiliated with, endorsed by, or sponsored by NASA. Astronomy Picture of the Day images, videos, explanations, and credits are provided by NASA APOD and/or credited third-party authors. Users are responsible for complying with applicable NASA/APOD and third-party media usage terms.
```

## Store capability justification draft

`runFullTrust` justification:

```text
APOD Wallpaper is a desktop personalization app. It uses full-trust desktop capabilities to integrate with Windows desktop wallpaper APIs, system tray behavior, and startup behavior requested by the user. These features are central to the app's purpose: previewing, downloading, and applying APOD images as the user's Windows wallpaper.
```

## App package notes

Before submission:

- Replace package publisher identity if Partner Center requires a certificate subject different from the local test publisher.
- Confirm app icon/logo assets meet Store requirements.
- Confirm `systemAIModels` capability remains removed.
- Confirm `runFullTrust` justification is ready.
- Run the MSIX packaging checklist in `docs/msix-packaging-checklist.md`.
- Run Windows App Certification Kit if available.
