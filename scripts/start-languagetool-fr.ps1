param(
    [int]$Port = 8081,
    [switch]$Public,
    [string]$AllowOrigin
)

$ErrorActionPreference = "Stop"

$version = "6.5"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$toolsRoot = Join-Path $repoRoot "tools\languagetool"
$ltRoot = Join-Path $toolsRoot "LanguageTool-$version"
$serverJar = Join-Path $ltRoot "languagetool-server.jar"
$coreJar = Join-Path $ltRoot "languagetool.jar"
$libsGlob = Join-Path $ltRoot "libs\*"

if (-not (Test-Path $serverJar)) {
    & (Join-Path $PSScriptRoot "setup-languagetool.ps1")
}

$classPath = "$serverJar;$coreJar;$libsGlob"
$javaArgs = @(
    "-cp", $classPath,
    "org.languagetool.server.HTTPServer",
    "--port", $Port
)

if ($Public) {
    $javaArgs += "--public"
}

if ($AllowOrigin) {
    $javaArgs += @("--allow-origin", $AllowOrigin)
}

Write-Host "LanguageTool server starting on http://localhost:$Port"
Write-Host "Call /v2/check with language=fr for French rules."
& java @javaArgs
