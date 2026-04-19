#Requires AutoHotkey v2.0
#SingleInstance Force

WM_COPYDATA := 0x4A
CMD := Map(
    "SetRtf", 1,
    "SetTitle", 8,
    "SetName", 11
)

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

templateText := "MODELE TEST - SLOTS []`n`n"
    . "Indication : bilan de controle.`n"
    . "La patiente presente [] au sein gauche.`n"
    . "On note aussi [] dans le creux axillaire.`n"
    . "Conclusion : aspect compatible avec [].`n`n"
    . "Le texte du modele vient de SendCopyData et doit rester considere comme deja corrige.`n"
    . "Cliquez dans un [] puis dictez ou tapez du texte manuellement pour tester la correction locale."

SendCopyData(target, CMD["SetTitle"], "Proofing Slots Demo")
SendCopyData(target, CMD["SetName"], "Trusted WM_COPYDATA scaffold")
SendCopyData(target, CMD["SetRtf"], PlainTextToRtf(templateText))

WinActivate "ahk_id " target

MsgBox(
    "Template loaded into RadEdit.`n`n"
    . "How to test:`n"
    . "1. Enable Proof in RadEdit and keep provider = LLM.`n"
    . "2. Click inside one of the [] slots.`n"
    . "3. Dictate or type text manually inside the slot.`n`n"
    . "The scaffold inserted by this script came from SendCopyData, so it should stay trusted and not be re-corrected until you edit inside a slot.",
    "RadEdit Proofing Slots Demo"
)

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

PlainTextToRtf(text) {
    rtf := "{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}\viewkind4\uc1\pard\f0\fs20 "

    Loop Parse text
    {
        ch := A_LoopField
        switch ch {
            case "`r":
                continue
            case "`n":
                rtf .= "\par`n"
            case "\":
                rtf .= "\\"
            case "{":
                rtf .= "\{"
            case "}":
                rtf .= "\}"
            default:
                code := Ord(ch)
                if (code >= 32 && code <= 126) {
                    rtf .= ch
                } else {
                    rtf .= "\u" code "?"
                }
        }
    }

    rtf .= "\par}"
    return rtf
}
