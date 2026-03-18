#Requires AutoHotkey v2.0
#SingleInstance Force

WM_COPYDATA := 0x4A
CMD := Map("SetFile", 3, "RequestTemp", 5)

global pendingPaths := []
global expectedPaths := 2
global errorText := ""
global rawExportPath := ""
global sanitizedExportPath := ""

target := WinExist("ahk_exe RadEdit.exe")
if !target {
    candidates := [
        A_ScriptDir "\..\bin\Debug\net8.0-windows7.0\RadEdit.exe",
        A_ScriptDir "\..\bin\Debug\net8.0-windows\RadEdit.exe"
    ]

    for exePath in candidates {
        if FileExist(exePath) {
            Run exePath
            WinWait "ahk_exe RadEdit.exe",, 5
            target := WinExist("ahk_exe RadEdit.exe")
            if target {
                break
            }
        }
    }
}

if !target {
    MsgBox "RadEdit window not found. Build or run RadEdit, then rerun this script."
    ExitApp
}

OnMessage(WM_COPYDATA, CopyDataHandler)

SetTimer(SendRequests, -50)
return

SendRequests() {
    global target, CMD, rawExportPath, sanitizedExportPath

    rtfPath := A_ScriptDir "\snippet-hotkeys-demo.rtf"
    if !FileExist(rtfPath) {
        MsgBox "Demo RTF not found: " rtfPath
        ExitApp
    }

    stamp := A_Now . "-" . A_MSec
    rawExportPath := A_Temp "\RadEditSnippetSanitize-" . stamp . "-raw.rtf"
    sanitizedExportPath := A_Temp "\RadEditSnippetSanitize-" . stamp . "-sanitized.rtf"

    try {
        if FileExist(rawExportPath) {
            FileDelete(rawExportPath)
        }
    }

    try {
        if FileExist(sanitizedExportPath) {
            FileDelete(sanitizedExportPath)
        }
    }

    SendCopyData(target, CMD["SetFile"], rtfPath)
    SendCopyData(target, CMD["RequestTemp"], rawExportPath)

    json := '{"path":"' . JsonEscape(sanitizedExportPath) . '","stripHiddenMarkers":true}'
    SendCopyData(target, CMD["RequestTemp"], json)

    ToolTip "Testing snippet metadata export cleanup..."
    SetTimer(ShowTimeout, -3000)
}

CopyDataHandler(wParam, lParam, msg, hwnd) {
    global pendingPaths, expectedPaths, errorText
    static CMD_TEMP_RESP := 6, CMD_ERROR := 7

    cmd := NumGet(lParam, 0, "UPtr")
    size := NumGet(lParam, A_PtrSize, "UInt")
    text := StrGet(NumGet(lParam, 2 * A_PtrSize, "Ptr"), size / 2, "UTF-16")

    if (cmd = CMD_TEMP_RESP) {
        pendingPaths.Push(text)
        if (pendingPaths.Length >= expectedPaths) {
            SetTimer(VerifyExports, -1)
        }
    } else if (cmd = CMD_ERROR) {
        errorText := text
        SetTimer(ShowError, -1)
    }

    return true
}

VerifyExports() {
    global pendingPaths, rawExportPath, sanitizedExportPath

    ToolTip

    failures := []

    if !HasValue(pendingPaths, rawExportPath) {
        failures.Push("Raw export path was not returned in TempFileResponse.")
    }
    if !HasValue(pendingPaths, sanitizedExportPath) {
        failures.Push("Sanitized export path was not returned in TempFileResponse.")
    }
    if !FileExist(rawExportPath) {
        failures.Push("Raw export file was not created.")
    }
    if !FileExist(sanitizedExportPath) {
        failures.Push("Sanitized export file was not created.")
    }

    rawText := ""
    sanitizedText := ""

    if (failures.Length = 0) {
        rawText := FileRead(rawExportPath)
        sanitizedText := FileRead(sanitizedExportPath)

        if !InStr(rawText, "@@BEGIN:SNIPPETS@@") || !InStr(rawText, "@@END:SNIPPETS@@") {
            failures.Push("Raw export is missing snippet markers.")
        }
        if !InStr(rawText, '"popupHotkey"') || !InStr(rawText, '"hotkey":"Ctrl+F12"') {
            failures.Push("Raw export is missing snippet JSON content.")
        }
        if InStr(sanitizedText, "@@BEGIN:SNIPPETS@@") || InStr(sanitizedText, "@@END:SNIPPETS@@") {
            failures.Push("Sanitized export still contains snippet marker labels.")
        }
        if InStr(sanitizedText, '"popupHotkey"') || InStr(sanitizedText, '"hotkey":"Ctrl+F12"') {
            failures.Push("Sanitized export still contains hidden snippet JSON.")
        }
        if !InStr(sanitizedText, "Snippet Hotkeys Demo (RTF + HTML + AHK)") {
            failures.Push("Sanitized export removed visible document content.")
        }
        if !InStr(sanitizedText, "Report:") {
            failures.Push("Sanitized export removed the report body marker.")
        }
        if (rawText = sanitizedText) {
            failures.Push("Raw and sanitized exports are identical.")
        }
    }

    if (failures.Length > 0) {
        message := "FAIL`n`n"
        for index, failure in failures {
            message .= index ". " failure
            if (index < failures.Length) {
                message .= "`n"
            }
        }
        message .= "`n`nRaw:`n" rawExportPath
        message .= "`n`nSanitized:`n" sanitizedExportPath
        MsgBox message, "RadEdit Snippet Sanitize Test"
        ExitApp
    }

    message := "PASS`n`n"
    message .= "Raw export kept snippet metadata.`n"
    message .= "Sanitized export removed snippet metadata and preserved visible document text.`n`n"
    message .= "Raw:`n" rawExportPath
    message .= "`n`nSanitized:`n" sanitizedExportPath
    MsgBox message, "RadEdit Snippet Sanitize Test"
    ExitApp
}

ShowError() {
    global errorText
    ToolTip
    MsgBox "RadEdit error: " errorText, "RadEdit Snippet Sanitize Test"
    ExitApp
}

ShowTimeout() {
    global pendingPaths, expectedPaths, errorText
    if (pendingPaths.Length < expectedPaths && errorText = "") {
        ToolTip
        MsgBox "No TempFileResponse received within 3 seconds. Received " pendingPaths.Length " of " expectedPaths ".", "RadEdit Snippet Sanitize Test"
        ExitApp
    }
}

HasValue(values, expected) {
    for _, value in values {
        if (value = expected) {
            return true
        }
    }
    return false
}

JsonEscape(text) {
    text := StrReplace(text, "\", "\\")
    text := StrReplace(text, '"', '\"')
    return text
}

SendCopyData(hwnd, command, text := "") {
    global WM_COPYDATA
    text := text . Chr(0)
    buf := Buffer(StrLen(text) * 2, 0)
    StrPut(text, buf, "UTF-16")
    cds := Buffer(3 * A_PtrSize, 0)
    NumPut("UPtr", command, cds, 0)
    NumPut("UInt", buf.Size, cds, A_PtrSize)
    NumPut("Ptr", buf.Ptr, cds, 2 * A_PtrSize)
    DllCall("user32\SendMessageW", "Ptr", hwnd, "UInt", WM_COPYDATA, "Ptr", A_ScriptHwnd, "Ptr", cds.Ptr, "Ptr")
}
