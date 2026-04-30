#Requires AutoHotkey v2.0
#SingleInstance Force

; Press Ctrl+Alt+F10 when RadEdit shows "Not responding" or Dragon loses its anchor.
; This script writes from outside RadEdit, so it can mark the log even when RadEdit's UI thread is blocked.

^!F10::{
    logDir := A_AppData "\RadEdit"
    logPath := logDir "\radedit-debug.log"
    DirCreate(logDir)

    activeTitle := ""
    activeProcess := ""
    activeClass := ""
    try activeTitle := WinGetTitle("A")
    try activeProcess := WinGetProcessName("A")
    try activeClass := WinGetClass("A")

    line := FormatTime(, "yyyy-MM-dd HH:mm:ss")
        . " USER_MARK freeze noticed."
        . " ActiveProcess=" . activeProcess
        . " ActiveClass=" . activeClass
        . " ActiveTitle=" . activeTitle
        . "`r`n"

    FileAppend(line, logPath, "UTF-8")
    SoundBeep(900, 80)
}
