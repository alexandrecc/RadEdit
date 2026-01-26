#Requires AutoHotkey v2.0
#SingleInstance Force

WM_COPYDATA := 0x4A
CMD := Map("SetFile", 3, "SetHtmlFile", 17, "SetTitle", 8, "SetDataContext", 20)

rtfPath := A_ScriptDir "\html-routing-demo.rtf"
htmlPath := A_ScriptDir "\html-routing-demo.html"

if !FileExist(rtfPath) || !FileExist(htmlPath) {
    MsgBox "Demo files not found in: " A_ScriptDir
    ExitApp
}

target := WinExist("ahk_exe RadEdit.exe")
if !target {
    radEditExe := A_ScriptDir "\..\bin\Debug\net8.0-windows\RadEdit.exe"
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

SendCopyData(target, CMD["SetFile"], rtfPath)
SendCopyData(target, CMD["SetHtmlFile"], htmlPath)
SendCopyData(target, CMD["SetTitle"], "SetDataContext + HTML Routing Demo")

Sleep 1200

ctx := '{"FIRST_NAME":"Jordan","LAST_NAME":"Lee","EMAIL":"jordan.lee@hospital.org","PHONE":"+1 (555) 234-9876","SMART_MENU":"PET/CT","SERVICE_TYPE":"ct","PRIORITY":"urgent","START_DATE":"2026-02-18","BUDGET":"$12,000","FEATURE_CMS":true,"FEATURE_SEO":false,"FEATURE_ANALYTICS":true,"FEATURE_ECOMMERCE":true,"DESCRIPTION":"Intermittent chest pain with shortness of breath.","ATTACHMENT":"prior-imaging.pdf","TERMS":true}'
SendCopyData(target, CMD["SetDataContext"], ctx)

MsgBox "SetDataContext sent. Verify HTML fields and RTF placeholders updated."
return

CopyDataHandler(wParam, lParam, msg, hwnd) {
    static CMD_ERROR := 7
    cmd := NumGet(lParam, 0, "UPtr")
    size := NumGet(lParam, A_PtrSize, "UInt")
    text := StrGet(NumGet(lParam, 2 * A_PtrSize, "Ptr"), size / 2, "UTF-16")
    if (cmd = CMD_ERROR) {
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
