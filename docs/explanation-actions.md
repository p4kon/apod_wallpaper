# MainPage APOD Explanation Actions

## Goal

Add compact actions to the MainPage APOD explanation block:

- Copy the currently displayed explanation body.
- Open the original APOD explanation in Google Translate.
- Persist an independent target translation language.

The target translation language is independent from the application UI language.

## Status

| Task | Status | Notes |
| --- | --- | --- |
| Branch created | Done | `feature/explanation-actions` |
| Source/navigation review | Done | MCP graph used for MainPage, localization, settings, APOD explanation, smoke tests |
| Settings persistence | Done | Added `TranslationTargetLanguage` to app settings |
| MainPage UI | Done | Added Copy, Translate, target language selector beside Explanation |
| Copy behavior | Done | Copies displayed explanation body only |
| Translate behavior | Done | Opens Google Translate in external browser |
| Localization | Done | Added EN/RU AppStrings keys |
| Smoke tests | Done | Added target language and URL checks |
| Release installer | Not started | Out of scope for this task |

## Behavioral Requirements

- Default target translation language is `ru`.
- Supported target languages: `ru`, `es`, `de`, `fr`, `it`, `pt`, `ja`.
- Invalid saved target language is normalized to `ru`.
- Translation source language is always `en`.
- The original APOD explanation remains the master text.
- The displayed explanation text is separate from the original text for future internal translation.
- Current Translate action does not modify the in-app explanation text.
- Long Google Translate URLs copy the explanation to clipboard and open Google Translate without text.
- UI language changes update labels, tooltips, and flyout names, but do not change target translation language.
