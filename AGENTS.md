\# Codex rules for this repository



\## Project

APOD Wallpaper is a WinUI desktop app.



\## Build command

dotnet build apod\_wallpaper.sln -c Release



\## Hard rules

\- Do not change README.md unless the task explicitly asks for documentation.

\- Do not change RELEASE\_NOTES.md unless the task explicitly asks for release notes.

\- Do not change Package.appxmanifest unless the task explicitly asks for manifest/version changes.

\- Do not change scripts/build-installer.ps1 unless the task explicitly asks for installer changes.

\- Do not change .csproj files unless explicitly asked.

\- Do not bump application version unless explicitly asked.

\- Do not touch bin/obj files.



\## Workflow

\- Before editing, run git status --short.

\- Keep the task small.

\- Do not repeat the same search/read cycle more than twice.

\- If output is too large, narrow the command instead of repeating it.

\- If context becomes unclear, stop and report.

\- Run at most one final build unless asked otherwise.



\## Done means

\- Show git diff --name-status.

\- Run dotnet build apod\_wallpaper.sln -c Release.

\- Give a short report of changed files and why.



\## Encoding and localization rules



\- All source/text files containing Russian text must be saved as UTF-8.

\- Do not use ANSI, Windows-1251, OEM-866, or transliteration for Russian UI strings.

\- If PowerShell output shows mojibake/garbled Cyrillic, do not assume the file is corrupted.

\- Re-read files with explicit UTF-8 encoding before editing Russian strings:

&#x20; `Get-Content -Encoding UTF8 <path>`

\- Prefer `git diff` and direct file edits over judging Cyrillic from console output.

\- Never replace correct Russian text with mojibake or escaped garbage.

\- Russian UI strings must remain readable Cyrillic in source files.

\- After editing localization strings, run:

&#x20; `dotnet build apod\_wallpaper.sln -c Release`

