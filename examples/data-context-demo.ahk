#Requires AutoHotkey v2.0
#SingleInstance Force

WM_COPYDATA := 0x4A
CMD := Map("SetDataContext", 20, "GetDataContext", 21)
global pendingResponses := []
global pendingKeys := []
global expectedResponses := 0
global errorText := ""
global logPath := A_ScriptDir "\data-context-demo.log"
global resultGui := 0

try FileDelete(logPath)
Log("Starting data context demo.")

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
    Log("RadEdit window not found.")
    ExitApp
}

OnMessage(WM_COPYDATA, CopyDataHandler)

Log("Found RadEdit hwnd=" target)
ToolTip "Sending SetDataContext + GetDataContext..."
SetTimer(SendRequests, -50)
return

CopyDataHandler(wParam, lParam, msg, hwnd) {
    global pendingResponses, expectedResponses, errorText
    static CMD_CONTEXT_RESP := 22, CMD_ERROR := 7
    cmd := NumGet(lParam, 0, "UPtr")
    size := NumGet(lParam, A_PtrSize, "UInt")
    text := StrGet(NumGet(lParam, 2 * A_PtrSize, "Ptr"), size / 2, "UTF-16")
    if (cmd = CMD_CONTEXT_RESP) {
        pendingResponses.Push(text)
        Log("DataContextResponse received: " text)
        if (expectedResponses > 0 && pendingResponses.Length >= expectedResponses) {
            SetTimer(ShowResponse, -1)
        }
    } else if (cmd = CMD_ERROR) {
        errorText := text
        Log("ErrorResponse received: " text)
        SetTimer(ShowError, -1)
    }
    return true
}

SendRequests() {
    global target, CMD, pendingResponses, pendingKeys, expectedResponses
    pendingResponses := []
    pendingKeys := ["patient.name.last", "study.accession", "flags.urgent"]
    expectedResponses := pendingKeys.Length
    ctxJson := '{' .
        '"patient":{"id":"123456","name":{"first":"Ana","last":"Quinn"},"tags":["outpatient","followup"]},' .
        '"study":{"accession":"ABC-2026-0001","modality":"CT","series":[{"id":"S1","desc":"Axial"},{"id":"S2","desc":"Coronal"}]},' .
        '"flags":{"urgent":true,"approved":false},' .
        '"measurements":{"sizes":[1.2,3.4,5.6]}' .
        '}'
    Log("Sending SetDataContext payload.")
    SendCopyData(target, CMD["SetDataContext"], ctxJson)
    for _, key in pendingKeys {
        Log("Sending GetDataContext payload: " key)
        SendCopyData(target, CMD["GetDataContext"], key)
    }
    ToolTip "Sent SetDataContext + GetDataContext. Waiting for response..."
    SetTimer(ShowTimeout, -2000)
}

ShowResponse() {
    global pendingResponses, pendingKeys
    ToolTip
    message := "DataContext responses:`n"
    for index, key in pendingKeys {
        value := index <= pendingResponses.Length ? pendingResponses[index] : "<missing>"
        message .= key ": " value
        if (index < pendingKeys.Length) {
            message .= "`n"
        }
    }
    ShowResult(message)
}

ShowError() {
    global errorText
    ToolTip
    ShowResult("RadEdit error: " errorText)
}

ShowTimeout() {
    global pendingResponses, expectedResponses, errorText
    if (pendingResponses.Length < expectedResponses && errorText = "") {
        ToolTip
        ShowResult("No DataContextResponse received within 2 seconds. Received " pendingResponses.Length " of " expectedResponses ".")
        Log("No response received within timeout. Received " pendingResponses.Length " of " expectedResponses ".")
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
    result := DllCall("user32\SendMessageW", "Ptr", hwnd, "UInt", WM_COPYDATA, "Ptr", A_ScriptHwnd, "Ptr", cds.Ptr, "Ptr")
    Log("SendCopyData cmd=" command " result=" result)
}

Log(message) {
    global logPath
    FileAppend(message "`n", logPath)
}

ShowResult(message) {
    global resultGui
    if (IsObject(resultGui)) {
        try resultGui.Destroy()
    }
    resultGui := Gui("+AlwaysOnTop +ToolWindow", "RadEdit DataContext")
    resultGui.AddText("w380", message)
    resultGui.AddButton("w80", "OK").OnEvent("Click", CloseResult)
    resultGui.OnEvent("Close", CloseResult)
    resultGui.Show()
}

CloseResult(*) {
    global resultGui
    if (IsObject(resultGui)) {
        resultGui.Destroy()
    }
    ExitApp
}
