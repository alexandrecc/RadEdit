# Version Log

## v0.2.6 - 2026-02-09

Changes since the previous version:

- Fixed HTML metadata parsing for `<meta name="radedit:context" ...>` so JSON now works directly in single-quoted `content` attributes (for example `content='{"patientId":"123456"}'`).
- Kept HTML-encoded quote support (`&quot;`) for backward compatibility in metadata payloads.
- Fixed `GetDataContext` response serialization so accented characters and symbols are returned as readable UTF-8 JSON (for example `É`, `+`) instead of escaped `\uXXXX`.
- Added `examples\data-context-accents-demo.ahk` to validate `SetDataContext`/`GetDataContext` with accents using raw WM_COPYDATA output (no AHK JSON parsing).

## v0.2.5 - 2026-02-06

Changes since the previous version:

- Added per-template global snippet hotkeys for RTF fragment insertion at the current RTF caret.
- Added optional global snippet popup hotkey with a menu showing each hotkey and insertion preview.
- Added snippet config loading from hidden RTF marker blocks (`@@BEGIN:SNIPPETS@@` / `@@END:SNIPPETS@@`).
- Added snippet config loading from HTML metadata (`<meta name="radedit:snippets" ...>` / `radedit:hotkeys`).
- Added warnings for conflicting snippet sources (HTML + RTF) and hotkey registration collisions.
- Added parsing cleanup so hidden RTF snippet payloads with leading/trailing `\par` still parse as JSON.
- Added a 10-hotkey demo bundle in `examples\snippet-hotkeys-demo.*` (RTF + HTML + AHK).

## v0.2.4 - 2026-02-05

Changes since the previous version:

- Added HTML metadata support for seeding the data context via `<meta name="radedit:context" ...>`.
- Added WebView2 JavaScript helpers: `setDataContext`, `getDataContext`, `updateView`, and `sendRtf`.
- Data context updates now sync into HTML after navigation and broadcast to popup WebViews.
- Added a standalone HTML demo for the data context + view helpers.
- Added an AutoHotkey launcher for the HTML data context demo.
- Fixed dictation routing so HTML focus without an editable target no longer suppresses RTF input.

## v0.2.3 - 2026-01-28

Changes since the previous version:

- SetHtmlFile now clears the HTML view when the payload is empty.
- Added an AutoHotkey demo that loads and clears the HTML view.

## v0.2.2 - 2026-01-28

Changes since the previous version:

- Added optional RequestTempFile JSON payload support, including `stripHiddenMarkers`.
- Added RTF export post-processing to remove @@BEGIN/@@END tokens when requested.
- Added an AutoHotkey demo that exports raw vs sanitized temp files.

## v0.2.1 - 2026-01-25

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

## v0.2.0 - 2026-01-16

Changes since the previous version:

- Split the top toolbar into two rows and left-align the action buttons.
- Added HTML URL bar with Go/Open/Clear, plus Back/Forward navigation.
- Collapsed HTML view to a minimal bar when no HTML is loaded.
- Added HTML view metadata support via `<meta name="radedit:view" ...>` with split/pop/full options.
- Added HTML view metadata demo files in `examples`.
- Window title now includes the app version.
