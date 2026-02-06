#Requires AutoHotkey v2.0

WM_COPYDATA := 0x4A
CMD := Map("SetFile", 3, "SetHtmlFile", 17, "SetTitle", 8, "GotoEnd", 14)

rtfPath := A_ScriptDir "\snippet-hotkeys-demo.rtf"
htmlPath := A_ScriptDir "\snippet-hotkeys-demo.html"

if !FileExist(rtfPath) || !FileExist(htmlPath) {
    MsgBox "Demo files not found in: " A_ScriptDir
    ExitApp
}

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

SendCopyData(target, CMD["SetFile"], rtfPath)
SendCopyData(target, CMD["SetHtmlFile"], htmlPath)
SendCopyData(target, CMD["SetTitle"], "Snippet Hotkeys Demo (10)")
SendCopyData(target, CMD["GotoEnd"], "")

MsgBox "Demo loaded.`n`nGlobal snippet hotkeys:`n  Ctrl+0, Ctrl+2 .. Ctrl+9, Ctrl+F12`nPopup menu:`n  Ctrl+1"
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
