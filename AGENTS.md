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

