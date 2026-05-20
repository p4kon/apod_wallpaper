# Product copy and onboarding text

Date: 2026-05-20

This document keeps Store copy, onboarding text, settings text, and in-app explanations consistent.

## Product positioning

One-line positioning:

```text
APOD Wallpaper turns NASA's Astronomy Picture of the Day into a calm, automatic Windows wallpaper experience.
```

Plain-language explanation:

```text
APOD Wallpaper helps you browse NASA's Astronomy Picture of the Day, preview available images, save them locally, and apply them as your desktop wallpaper.
```

Automatic wallpaper explanation:

```text
When automatic updates are enabled, APOD Wallpaper checks for the latest available APOD image and applies it as your desktop wallpaper. If the latest APOD is a video or is not available yet, the app skips it and tries again later.
```

API key explanation:

```text
A NASA API key is optional. The app can work without one by reading public APOD pages, but adding a personal API key can improve reliability and avoid shared demo-key limits.
```

Video-day explanation:

```text
Some APOD entries are videos or interactive pages instead of images. These days are marked as video and are not applied as wallpaper.
```

## First-run onboarding

Goal:

Explain the app in 2-3 short steps without blocking the user.

Suggested first-run screen / dialog:

Title:

```text
Welcome to APOD Wallpaper
```

Body:

```text
Browse NASA's Astronomy Picture of the Day by date, preview available images, and apply them as your Windows wallpaper.
```

Bullet points:

```text
Automatic updates can check for new APOD images and apply them for you.
Video days are skipped because they cannot be used as desktop wallpaper.
A NASA API key is optional and can be added later in Settings.
```

Primary button:

```text
Start browsing
```

Secondary button:

```text
Open Settings
```

Do not show again checkbox:

```text
Don't show this again
```

## Automatic updates onboarding

Short version:

```text
Automatically check for today's APOD and apply it when an image is available.
```

Long version:

```text
APOD Wallpaper can quietly check for the latest APOD image and update your desktop wallpaper. If today's APOD has not been published yet, or if it is a video, the app will skip it and try again later.
```

Toggle label:

```text
Auto-update wallpaper
```

Toggle helper text:

```text
Checks for the latest available APOD image and applies it automatically.
```

Auto-on button text:

```text
Auto On
```

Auto-off button text:

```text
Auto Off
```

Status copy when enabled:

```text
Automatic wallpaper updates are enabled.
```

Status copy when disabled:

```text
Automatic wallpaper updates are disabled.
```

## API key copy

Settings card title:

```text
NASA API Key
```

Settings card description:

```text
Optional. Add a personal NASA API key to improve reliability and avoid shared demo-key limits.
```

Configure button:

```text
Configure
```

API dialog title:

```text
NASA API Key
```

API dialog body:

```text
APOD Wallpaper can work without an API key by using public APOD pages. A personal NASA API key is optional, but it can make APOD requests more reliable and avoid shared demo-key limits.
```

API field placeholder:

```text
Paste your NASA API key
```

Save button:

```text
Save key
```

Remove button:

```text
Remove key
```

Get key link:

```text
Get a free NASA API key
```

Success message:

```text
NASA API key saved.
```

Removed message:

```text
NASA API key removed. APOD Wallpaper will continue using public APOD pages.
```

Invalid key message:

```text
This API key could not be validated. Check the key and try again, or continue without one.
```

Rate-limit message:

```text
NASA API requests are temporarily limited. APOD Wallpaper will use public APOD pages when possible.
```

## Video and unavailable day copy

Video placeholder title:

```text
Video day
```

Video placeholder body:

```text
This APOD entry is a video or interactive page, so it cannot be applied as wallpaper.
```

Unavailable placeholder title:

```text
Not available yet
```

Unavailable placeholder body:

```text
NASA has not published an APOD image for this date yet. Try again later.
```

Unknown placeholder title:

```text
Unchecked date
```

Unknown placeholder body:

```text
Select this date to check whether an APOD image is available.
```

Network error title:

```text
Could not load APOD
```

Network error body:

```text
Check your internet connection and try again.
```

## Calendar legend copy

Recommended labels:

```text
Local
Available
Video
Unchecked
```

Tooltip copy, if added later:

```text
Local: image is saved on this device.
Available: image is available online.
Video: APOD entry is not an image.
Unchecked: not checked yet.
```

## Action button copy

Open APOD page:

```text
NASA
```

Download:

```text
Download
```

Apply:

```text
Apply
```

When applying:

```text
Applying...
```

When downloading:

```text
Downloading...
```

Download success:

```text
Image saved locally.
```

Apply success:

```text
Wallpaper applied.
```

Apply unavailable:

```text
This date cannot be applied as wallpaper.
```

## Settings copy

Start with Windows:

```text
Start with Windows
```

Start with Windows helper:

```text
Launch APOD Wallpaper automatically when you sign in.
```

Close behavior:

```text
Close behavior
```

Close behavior helper:

```text
Choose whether the close button hides the app to the tray or exits it.
```

Download folder:

```text
Download folder
```

Download folder helper:

```text
Where APOD images are saved.
```

Wallpaper style:

```text
Wallpaper style
```

Wallpaper style helper:

```text
Choose how images fit your screen.
```

## Store listing copy blocks

Store short description:

```text
Browse NASA APOD images and apply them as your Windows wallpaper.
```

Store long description addition:

```text
Automatic updates are optional. When enabled, APOD Wallpaper checks for the latest available APOD image and applies it as your desktop wallpaper. Video entries and unpublished dates are skipped automatically.
```

API key Store note:

```text
NASA API key support is optional. The app can work without a key, but a personal key may improve reliability.
```

NASA disclaimer:

```text
APOD Wallpaper is independent and is not affiliated with, endorsed by, or sponsored by NASA. APOD media and explanations are provided by NASA APOD and/or credited authors.
```

## Privacy copy

Short privacy summary:

```text
APOD Wallpaper stores settings locally on your device and contacts NASA APOD services to load images and metadata. It does not include ads or analytics.
```

API key privacy line:

```text
If you add a NASA API key, it is stored locally and used only for NASA APOD requests.
```

Wallpaper behavior disclosure:

```text
If automatic updates are enabled, APOD Wallpaper can change your desktop wallpaper without opening the main window.
```

## Tone rules

Use:

- "automatic updates" instead of "scheduler" in user-facing copy
- "video day" instead of "unsupported media" where possible
- "not available yet" instead of "404"
- "NASA API key is optional" every time API key is introduced

Avoid:

- technical HTTP status text in normal UI
- implying the app is official NASA software
- saying all APOD images are NASA-owned
- promising every APOD can become wallpaper
