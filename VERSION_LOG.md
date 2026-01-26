# Version Log

## v0.2.1

Changes since the previous version:

- Added LanguageTool integration with issue underlines, hover suggestions, and ignored-rule tracking.
- Added scripts to download and start the local LanguageTool server.
- RequestHtmlFile now returns ErrorResponse when no HTML view is active (no more RTF fallback).
- Constrained the LanguageTool hover menu to stay within the current screen bounds.
- Added an AutoHotkey script to test RequestHtmlFile responses.
- Added SetDataContext syncing into HTML fields and RTF regions, plus a demo script.
- Disabled WebView2 cache for local HTML files while keeping default caching for remote URLs.
- Added WebView2 popup handling so new windows keep local mappings and routing.
- Kept RTF toolbar commands active in HTML mode and removed auto-disabling of HTML.
- Updated RTF region markers to use @@BEGIN/@@END to avoid Dragon bracket parsing.
- Added WebView2 popup test assets for local relative path validation.

## v0.2.0

Changes since the previous version:

- Split the top toolbar into two rows and left-align the action buttons.
- Added HTML URL bar with Go/Open/Clear, plus Back/Forward navigation.
- Collapsed HTML view to a minimal bar when no HTML is loaded.
- Added HTML view metadata support via `<meta name="radedit:view" ...>` with split/pop/full options.
- Added HTML view metadata demo files in `examples`.
- Window title now includes the app version.
