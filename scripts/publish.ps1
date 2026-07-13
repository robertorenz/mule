# Builds the release artifacts for M.U.L.E. Colony:
#   1. publish\portable\MULE-Colony.exe  - single-file, self-contained, compressed
#   2. publish\installer\MULE-Colony-Setup.exe - Inno Setup installer
#
# Usage:  pwsh scripts\publish.ps1
# Requires: .NET 9 SDK, and Inno Setup 6 for the installer step.

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root "src\Mule.Game\Mule.Game.csproj"
$pub  = Join-Path $root "publish"

Remove-Item $pub -Recurse -Force -ErrorAction SilentlyContinue

$common = @(
    "-c", "Release", "-r", "win-x64", "--self-contained", "true",
    "-p:DebugType=none", "-p:DebugSymbols=false"
)

Write-Host "== Single-file portable exe =="
dotnet publish $proj @common `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o "$pub\portable"
Rename-Item "$pub\portable\Mule.Game.exe" "MULE-Colony.exe"

Write-Host "== Folder publish (installer payload) =="
dotnet publish $proj @common -p:PublishSingleFile=false -o "$pub\app"

Write-Host "== Installer (Inno Setup) =="
$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (Test-Path $iscc) {
    & $iscc (Join-Path $root "installer\mule.iss")
} else {
    Write-Warning "Inno Setup not found at $iscc - skipping installer. Portable exe is ready."
}

Write-Host "`nDone. Artifacts:"
Write-Host "  $pub\portable\MULE-Colony.exe"
Write-Host "  $pub\installer\MULE-Colony-Setup.exe"
