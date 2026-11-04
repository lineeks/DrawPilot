param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$BuildShell
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$hostOutput = Join-Path $root "src\CorelDRAWTool.Host\bin\$Configuration\net48"
$gmsSource = Join-Path $root "shell\build\CorelDRAWTool.gms"
$gmsTargetDir = Join-Path $env:APPDATA "Corel\CorelDRAW Technical Suite 2025\Draw\GMS"
$gmsTarget = Join-Path $gmsTargetDir "CorelDRAWTool.gms"
$installDir = Join-Path $gmsTargetDir "CorelDRAWTool\app"

& (Join-Path $PSScriptRoot "build.ps1") -Configuration $Configuration

if ($BuildShell) {
    & (Join-Path $PSScriptRoot "build-shell.ps1")
}

$hostExe = Join-Path $hostOutput "CorelDRAWTool.Host.exe"
if (-not (Test-Path $hostExe)) {
    throw "Host build output not found: $hostExe"
}

if (Test-Path $installDir) {
    Remove-Item $installDir -Recurse -Force
}

New-Item -ItemType Directory -Path $installDir -Force | Out-Null
Copy-Item (Join-Path $hostOutput "*") $installDir -Recurse -Force
Write-Host "Host deployed to $installDir" -ForegroundColor Green

if (Test-Path $gmsSource) {
    New-Item -ItemType Directory -Path $gmsTargetDir -Force | Out-Null
    Copy-Item $gmsSource $gmsTarget -Force
    Write-Host "GMS deployed to $gmsTarget" -ForegroundColor Green
}
else {
    Write-Host "GMS not found at $gmsSource; skipped." -ForegroundColor Yellow
    Write-Host "Follow shell\README.md to create it the first time." -ForegroundColor Yellow
}
