#Requires AutoHotkey v2.0
#SingleInstance Force
Persistent
WM_COPYDATA := 0x4A
CMD := Map("RequestHtmlFile", 18)

target := WinExist("ahk_exe RadEdit.exe")
if !target {
    MsgBox "RadEdit window not found"
    ExitApp
}

responseReceived := false
OnMessage(WM_COPYDATA, CopyDataHandler)
SendCopyData(target, CMD["RequestHtmlFile"], "RadEditHtmlTest")
SetTimer(TimeoutHandler, -3000)
return

CopyDataHandler(wParam, lParam, msg, hwnd) {
    static CMD_HTML_RESP := 19, CMD_ERROR := 7
    cmd := NumGet(lParam, 0, "UPtr")
    size := NumGet(lParam, A_PtrSize, "UInt")
    text := StrGet(NumGet(lParam, 2*A_PtrSize, "Ptr"), size / 2, "UTF-16")
    if (cmd = CMD_HTML_RESP) {
        responseReceived := true
        MsgBox "HTML file created: " text
        ExitApp
    } else if (cmd = CMD_ERROR) {
        responseReceived := true
        MsgBox "RadEdit error: " text
        ExitApp
    }
    return true
}

TimeoutHandler() {
    if (!responseReceived) {
        MsgBox "No response from RadEdit (timeout)."
        ExitApp
    }
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
