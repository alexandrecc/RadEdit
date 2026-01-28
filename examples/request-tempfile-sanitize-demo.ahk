#Requires AutoHotkey v2.0
#SingleInstance Force

WM_COPYDATA := 0x4A
CMD := Map("SetFile", 3, "RequestTemp", 5)

global pendingPaths := []
global expectedPaths := 2
global errorText := ""

target := WinExist("ahk_exe RadEdit.exe")
if !target {
    radEditExe := A_ScriptDir "\..\bin\Debug\net8.0-windows\RadEdit.exe"
    if !FileExist(radEditExe) {
        radEditExe := A_ScriptDir "\..\bin\Debug\net8.0-windows7.0\RadEdit.exe"
    }
    if FileExist(radEditExe) {
        Run radEditExe
        WinWait "ahk_exe RadEdit.exe",, 4
        target := WinExist("ahk_exe RadEdit.exe")
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
    global target, CMD

    rtfPath := A_ScriptDir "\html-routing-demo.rtf"
    if !FileExist(rtfPath) {
        MsgBox "Demo RTF not found: " rtfPath
        ExitApp
    }

    SendCopyData(target, CMD["SetFile"], rtfPath)

    baseName := "RadEditTempDemo-" . A_Now
    rawName := baseName . "-raw"
    sanitizedName := baseName . "-sanitized"

    SendCopyData(target, CMD["RequestTemp"], rawName)

    json := '{"path":"' . sanitizedName . '","stripHiddenMarkers":true}'
    SendCopyData(target, CMD["RequestTemp"], json)

    ToolTip "Requested temp files (raw + sanitized). Waiting for responses..."
    SetTimer(ShowTimeout, -2000)
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
            SetTimer(ShowResponse, -1)
        }
    } else if (cmd = CMD_ERROR) {
        errorText := text
        SetTimer(ShowError, -1)
    }

    return true
}

ShowResponse() {
    global pendingPaths
    ToolTip
    message := "Temp files created:`n"
    for index, path in pendingPaths {
        message .= path
        if (index < pendingPaths.Length) {
            message .= "`n"
        }
    }
    MsgBox message
    ExitApp
}

ShowError() {
    global errorText
    ToolTip
    MsgBox "RadEdit error: " errorText
    ExitApp
}

ShowTimeout() {
    global pendingPaths, expectedPaths, errorText
    if (pendingPaths.Length < expectedPaths && errorText = "") {
        ToolTip
        MsgBox "No TempFileResponse received within 2 seconds. Received " pendingPaths.Length " of " expectedPaths "."
        ExitApp
    }
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
