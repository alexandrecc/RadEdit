#Requires AutoHotkey v2.0
#SingleInstance Force

WM_COPYDATA := 0x4A
CMD := Map("SetDataContext", 20, "GetDataContext", 21)
global gotResponse := false

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

accentE := Chr(0x00C9)
procLabel := "ANGIO SCAN CÉRÉBRAL C+"
ctxJson := '{"__mode":"replace","data":{' .
    '"proc":"' . procLabel . '",' .
    '"modal":"CT",' .
    '"loc":"Urgence",' .
    '"first":"1",' .
    '"reqnb":"RA2026016177",' .
    '"patdos":"465684",' .
    '"patnom":"Couturier, Serge",' .
    '"safeproc":"' . procLabel . '",' .
    '"studydate":"07/02/2026"' .
    '}}'

SendCopyData(target, CMD["SetDataContext"], ctxJson)
SendCopyData(target, CMD["GetDataContext"], "")
SetTimer(ShowTimeout, -2000)
return

CopyDataHandler(wParam, lParam, msg, hwnd) {
    global gotResponse
    static CMD_CONTEXT_RESP := 22, CMD_ERROR := 7
    cmd := NumGet(lParam, 0, "UPtr")
    size := NumGet(lParam, A_PtrSize, "UInt")
    text := StrGet(NumGet(lParam, 2 * A_PtrSize, "Ptr"), size / 2, "UTF-16")

    if (cmd = CMD_CONTEXT_RESP) {
        gotResponse := true
        MsgBox "DataContext (brut):`n`n" text
        ExitApp
    }

    if (cmd = CMD_ERROR) {
        gotResponse := true
        MsgBox "RadEdit error:`n`n" text
        ExitApp
    }

    return true
}

ShowTimeout() {
    global gotResponse
    if !gotResponse {
        MsgBox "No DataContextResponse received within 2 seconds."
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
