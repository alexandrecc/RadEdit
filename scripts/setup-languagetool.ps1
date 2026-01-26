param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$version = "6.5"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$toolsRoot = Join-Path $repoRoot "tools\languagetool"
$zipName = "LanguageTool-$version.zip"
$zipPath = Join-Path $toolsRoot $zipName
$extractDir = Join-Path $toolsRoot "LanguageTool-$version"
$downloadUrl = "https://languagetool.org/download/$zipName"

New-Item -ItemType Directory -Force -Path $toolsRoot | Out-Null

if ($Force) {
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
    if (Test-Path $extractDir) {
        Remove-Item $extractDir -Recurse -Force
    }
}

if (-not (Test-Path $zipPath)) {
    Write-Host "Downloading $downloadUrl..."
    try {
        Start-BitsTransfer -Source $downloadUrl -Destination $zipPath
    } catch {
        Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath
    }
}

if (-not (Test-Path $extractDir)) {
    Write-Host "Extracting $zipName..."
    Expand-Archive -Path $zipPath -DestinationPath $toolsRoot -Force
}

Write-Host "LanguageTool $version is ready at $extractDir"
