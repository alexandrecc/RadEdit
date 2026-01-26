# RadEdit

RadEdit is a minimal WinForms host around the `RichTextBox` control designed to work in tandem with automation tools such as AutoHotkey. The application exposes a small set of commands over `WM_COPYDATA`, allowing external scripts to push RTF content in, insert snippets at the current selection, or pull the current document out to a temporary file.

## Building / Running

```powershell
# build
dotnet build

# run for development
dotnet run --project RadEdit.csproj
```

A Debug build targets `net8.0-windows` and produces `bin\Debug\net8.0-windows\RadEdit.exe`.

## LanguageTool (French) local server

RadEdit does not bundle LanguageTool, but you can run a local French check server for autocorrection experiments.

```powershell
# download + unpack (stored under tools\languagetool)
./scripts/setup-languagetool.ps1

# start server on localhost:8081
./scripts/start-languagetool-fr.ps1
```

Quick sanity check:

```powershell
Invoke-WebRequest -Uri "http://localhost:8081/v2/check" -Method Post -Body @{
  text = "Je veut un example."
  language = "fr"
} | Select-Object -ExpandProperty Content
```

When integrating, call `/v2/check` with `language=fr` to force French rules.

## Shipping Single-File (no bundled WebView2 runtime)

Publish a single-file release that uses the installed WebView2 Evergreen Runtime:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true
```

The output is `bin\Release\net8.0-windows7.0\win-x64\publish\RadEdit.exe`. Make sure the target machine already has the WebView2 Runtime installed.

## Window Overview

- Top tool strip contains:
  - Title label (left)
  - Name label (center)
  - Bold/Italic/Underline buttons that operate on the current selection
- Main editor surface is a standard `RichTextBox` with URL detection disabled and vertical scroll bars.

## WM_COPYDATA Commands

RadEdit listens for `WM_COPYDATA` messages whose `dwData` matches one of the following command identifiers. The payload must be UTF-16 text (trailing null is optional).

| Command | `dwData` | Payload | Description |
| --- | --- | --- | --- |
| `SetRtfText` | 1 | RTF text | Replaces the entire document with the supplied RTF. Empty payload clears the editor. |
| `InsertRtfText` | 2 | RTF text | Inserts/overwrites at the current selection using `SelectedRtf`. |
| `SetRtfFile` | 3 | File path | Loads an RTF file from disk into the editor (replaces content). Relative paths resolve against the RadEdit process working directory. |
| `InsertRtfFile` | 4 | File path | Inserts the contents of the RTF file at the current selection. |
| `RequestTempFile` | 5 | Optional path or filename | Saves the current document to an RTF file and returns the absolute path via a `TempFileResponse` (see below). If the payload is relative, the file is created under `%TEMP%`; otherwise an absolute path is respected. Without a payload, a file named after the window title is generated under `%TEMP%`. |
| `TempFileResponse` | 6 | File path | Response emitted by RadEdit for `RequestTempFile`. |
| `ErrorResponse` | 7 | Error text | Response emitted if a command throws an exception (file not found, bad payload, etc.). |
| `SetTitle` | 8 | Title text | Updates the title label on the tool strip. The window caption remains `RadEdit`. |
| `GetTitle` | 9 | *(ignored)* | RadEdit responds with `TitleResponse` containing the current title. |
| `TitleResponse` | 10 | Title text | Response emitted for `GetTitle`. |
| `SetName` | 11 | Name text | Updates the centered name label on the tool strip. |
| `GetName` | 12 | *(ignored)* | RadEdit responds with `NameResponse` containing the current name. |
| `NameResponse` | 13 | Name text | Response emitted for `GetName`. |
| `GotoEnd` | 14 | *(ignored)* | Moves the caret to the end of the document (ignores trailing whitespace) and scrolls into view. |
| `FixFont` | 15 | Optional font name, size, or `name;size` | Applies a font face/size to the entire document. Empty payload defaults to Arial 10. |
| `CleanUpEnd` | 16 | *(ignored)* | Trims trailing whitespace at the end of the document. |
| `SetHtmlFile` | 17 | File path | Loads the HTML file in WebView2 and switches the UI into HTML mode. |
| `RequestHtmlFile` | 18 | Optional path or filename | Exports the current DOM (including filled form values) to an HTML file and returns the absolute path via `HtmlFileResponse`. Uses the same `%TEMP%`/absolute path rules as `RequestTempFile`. If HTML mode is not active, RadEdit falls back to `RequestTempFile` behavior and emits `TempFileResponse`. |
| `HtmlFileResponse` | 19 | File path | Response emitted by RadEdit for `RequestHtmlFile`. |
| `SetDataContext` | 20 | JSON object | Merges the supplied JSON object into the current data context. Empty payload clears the context. To replace entirely, send `{"__mode":"replace","data":{...}}`. |
| `GetDataContext` | 21 | Optional key path | Returns the full data context JSON when empty, or a single value when given a dotted path like `patient.id`. Missing keys return `null`. |
| `DataContextResponse` | 22 | JSON | Response emitted by RadEdit for `GetDataContext`. |

> **Note**: RadEdit does not currently emit responses for commands other than `RequestTempFile`/`RequestHtmlFile`/`GetTitle`/`GetName`/`GetDataContext`, but callers should always check for an `ErrorResponse` to surface issues.

## AutoHotkey Integration Example

```ahk
WM_COPYDATA := 0x4A
CMD := Map("SetRtf", 1, "InsertRtf", 2, "SetFile", 3, "InsertFile", 4
          , "RequestTemp", 5, "SetTitle", 8, "GetTitle", 9, "SetName", 11
          , "GetName", 12, "GotoEnd", 14, "FixFont", 15, "CleanUpEnd", 16
          , "SetHtmlFile", 17, "RequestHtmlFile", 18
          , "SetDataContext", 20, "GetDataContext", 21)

target := WinExist("ahk_exe RadEdit.exe")
if !target {
    MsgBox "RadEdit window not found"
    ExitApp
}

; replace document by loading an RTF snippet
SendCopyData(target, CMD["SetRtf"], "{\rtf1\ansi Hello, world!}")

; update labels
SendCopyData(target, CMD["SetTitle"], "Patient Summary")
SendCopyData(target, CMD["SetName"], "Dr. Example")

; set and read a data context value
ctxJson := '{"patientId":"123456","accession":"ABC-2026-0001"}'
SendCopyData(target, CMD["SetDataContext"], ctxJson)
SendCopyData(target, CMD["GetDataContext"], "patientId")

; request a temp file and capture the response
OnMessage(WM_COPYDATA, CopyDataHandler)
SendCopyData(target, CMD["RequestTemp"], "RadEditOutput")
return

CopyDataHandler(wParam, lParam, msg, hwnd) {
    static CMD_TEMP := 5, CMD_TEMP_RESP := 6, CMD_HTML_RESP := 19, CMD_CONTEXT_RESP := 22, CMD_ERROR := 7
    cmd := NumGet(lParam, 0, "UPtr")
    size := NumGet(lParam, A_PtrSize, "UInt")
    text := StrGet(NumGet(lParam, 2*A_PtrSize, "Ptr"), size/2, "UTF-16")
    if (cmd = CMD_TEMP_RESP) {
        MsgBox "Temp file created: " text
    } else if (cmd = CMD_HTML_RESP) {
        MsgBox "HTML file created: " text
    } else if (cmd = CMD_CONTEXT_RESP) {
        MsgBox "Data context: " text
    } else if (cmd = CMD_ERROR) {
        MsgBox "RadEdit error: " text
    }
    return true
}

SendCopyData(hwnd, command, text := "") {
    text := text . Chr(0)
    buf := Buffer(StrLen(text) * 2, 0)
    StrPut(text, buf, "UTF-16")
    cds := Buffer(3 * A_PtrSize, 0)
    NumPut("UPtr", command, cds, 0)
    NumPut("UInt", buf.Size, cds, A_PtrSize)
    NumPut("Ptr", buf.Ptr, cds, 2 * A_PtrSize)
    DllCall("user32\SendMessageW", "Ptr", hwnd, "UInt", WM_COPYDATA, "Ptr", A_ScriptHwnd, "Ptr", cds.Ptr, "Ptr")
}
```

## Data Context Demo (AutoHotkey)

Run `examples\data-context-demo.ahk` to send a `SetDataContext` JSON payload and request the `patientId` back via `GetDataContext`. A `DataContextResponse` message box confirms the round trip.

## HTML to RTF Routing

When HTML mode is active, form fields can push text into hidden regions in the RTF. Define the region in the RTF with hidden markers:

```
{\v [[BEGIN:PE_RULE]]}
(auto)
{\v [[END:PE_RULE]]}
```

Then add `data-target-region` in the HTML. When the field changes, RadEdit replaces the text between the markers with the mapped text or the raw value.

```
<select data-field="PE_PRESENT"
        data-target-region="PE_RULE"
        data-map='{"yes":"Pulmonary embolism is present.","no":"No pulmonary embolism."}'>
  <option value="no">No</option>
  <option value="yes">Yes</option>
</select>
```

Checkboxes can use `data-map` with `true`/`false` keys. A working demo lives in `examples\html-routing-demo.html`, `examples\html-routing-demo.rtf`, and `examples\html-routing-demo.ahk`.

## HTML View Metadata

Local HTML files can include a meta tag that tells RadEdit how to open the HTML view when the file loads. This is only read for local HTML files (not remote URLs).

```
<meta name="radedit:view" content="mode=split;size=50">
```

Supported keys:

- `mode`: `split`, `pop`, or `full`.
- `size`: percent for split mode (0-100). Defaults to 50.
- `monitor`: 1-based monitor index for `pop`/`full` (falls back to primary monitor).
- `x`, `y`, `width`, `height`: pop window bounds (pixels).

Examples live in:

- `examples\html-view-meta-split-50.html`
- `examples\html-view-meta-pop.html`
- `examples\html-view-meta-full-monitor-1.html`

## Troubleshooting

- **SetRtf/InsertRtf fails silently**: ensure the payload is valid RTF (e.g., starts with `{\rtf`).
- **File operations fail**: RadEdit relays exceptions via `ErrorResponse`. Check the returned text for the detailed message (e.g., file not found).
- **Temp file command**: the recipient handle (`wParam`) must be non-zero so RadEdit knows where to send the response.

## Future Ideas

- Macro management and toolbar integration (previous experiment removed for now).
- Additional status reporting (caret position, selection info).
- Optional plain-text commands for quick snippets.
