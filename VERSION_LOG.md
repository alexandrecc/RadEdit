# Version Log

## v0.2.9 - 2026-04-19

Changes since the previous version:

- Reworked proofreading around a stateful document model so trusted `WM_COPYDATA` scaffold text is not re-corrected, `[]` placeholders become tracked slots, and dictated text only proofs the relevant local sentence or slot context.
- Added slot policies for `[]` placeholders so heading/title slots stay protected while sentence-embedded slots can still proof the surrounding sentence when that makes grammatical sense.
- Reused unchanged LLM proofing units across edits and added stable in-memory dismissal keys so unchanged LLM suggestions no longer reappear immediately after being ignored.
- Fixed proofreading apply behavior so popup applies leave the caret at the corrected text while toolbar and hotkey applies preserve the logical caret offset through the replacement.
- Simplified the proofreading UI: `Proof` now lives next to `Pop RTF`, the current loaded model display sits beside it, only the suggestion list plus `Apply` and `Ignore` remain on the lower bar, and the old flickering status/provider/navigation controls are hidden.
- Added focused proofreading hotkeys `F11` = Apply and `F12` = Ignore while keeping the existing global `Ctrl+Alt+F11` / `Ctrl+Alt+F12` hotkeys, and exposed both shortcut paths in the button tooltips.
- Added runtime LLM model discovery from `/api/v1/models`, only use models that already report `loaded_instances`, show `No loaded model on server` when none are ready, retry discovery if the current model stops responding, and display the server's model `display_name` beside `Proof`.
- Added proof-status availability text beside `Proof` for server/model failures and kept a visible inactive caret plus one-click activation/caret placement in the editor even when RadEdit is not focused.

## v0.2.8 - 2026-04-06

Changes since the previous version:

- Added a selectable proofreading provider to the correction bar so users can switch between `LanguageTool` and a local LM Studio-backed LLM.
- Added a sentence-level LM Studio proofreading path that calls the local OpenAI-compatible `/v1/completions` API using the configured local LLM model, asks for conservative grammar-only fixes, and converts the corrected sentence back into accept/reject suggestions inside RadEdit.
- Kept the existing correction workflow for the LLM path: underlines, hover menu, Prev/Next navigation, Apply, Ignore, and proofreading hotkeys all continue to work with per-suggestion acceptance.
- Added persisted proofreading settings in `%APPDATA%\RadEdit\proofing-settings.json` for the enabled state, selected provider, LM Studio base URL, and model id.
- Improved suggestion handling so empty-string replacements now surface as `(delete)` and can still be applied from the combo box or hover menu.
- Restored the default LM Studio proofreading model to `qwen3.5-9b-claude-4.6-opus-reasoning-distilled-v2`.
- Stopped treating bare structured LLM replies like `{}` as visible proofreading corrections, and added debug logging for invalid LLM replies plus generated LLM suggestions.
- Added automatic migration from the temporary `gemma-4-31b-it` default back to the restored Qwen default, persist that normalized setting, and now log LLM HTTP/model-load failures plus unchanged LLM responses.
- Tightened the LM Studio JSON contract so the model must explicitly report whether it changed a gender/number agreement; if not, RadEdit now keeps the original sentence even if `corrected` contains other edits.
- Disabled runtime debug log writing by default, so HTML routing and LLM proofing no longer append to the debug log during normal RadEdit runs.
- Changed first-run proofing defaults so `Proof` starts unchecked and the default proofing provider/model are the local Qwen 9B LM Studio path.
- Added `%APPDATA%\RadEdit\config.json` to persist the main window size/position and the `Proof` checkbox state across launches.
- Changed the default LM Studio endpoint to `https://llm.radedit.org` and auto-migrate persisted proofing settings that still point to the previous IP-based or HTTP defaults.

## v0.2.7 - 2026-03-17

Changes since the previous version:

- Fixed `RequestTempFile` sanitization so `stripHiddenMarkers=true` now removes hidden `@@BEGIN:SNIPPETS@@ ... @@END:SNIPPETS@@` metadata blocks from exported RTF instead of only stripping the marker labels.
- Added `examples\request-tempfile-snippet-sanitize-test.ahk` to verify raw vs sanitized `RequestTempFile` export behavior for hidden snippet metadata.

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
