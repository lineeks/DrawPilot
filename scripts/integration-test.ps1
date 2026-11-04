$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "src\CorelDRAWTool.Host\bin\Release\net48"

if (-not (Get-Process CorelDRW -ErrorAction SilentlyContinue)) {
    Write-Host "CorelDRAW is not running." -ForegroundColor Yellow
    exit 1
}

foreach ($name in @("Corel.Interop.VGCore.dll", "CorelDRAWTool.Core.dll", "CorelDRAWTool.Corel.dll")) {
    $path = Join-Path $output $name
    if (-not (Test-Path $path)) {
        throw "Missing build output: $path. Run scripts\build.ps1 first."
    }
    [Reflection.Assembly]::LoadFrom($path) | Out-Null
}

$session = [CorelDRAWTool.Corel.CorelDrawSession]::Attach([TimeSpan]::FromSeconds(5))
try {
    $app = $session.GetApplicationSummary()
    $doc = $session.GetActiveDocumentSummary()

    Write-Host ("CorelDRAW: {0}" -f $app.DisplayVersion)
    Write-Host ("Program: {0}" -f $app.ProgramPath)
    Write-Host ("Has document: {0}" -f $doc.HasDocument)
    Write-Host ("Pages: {0}" -f $doc.PageCount)
    Write-Host ("Layers: {0}" -f $doc.LayerCount)
    Write-Host ("Selected shapes: {0}" -f $doc.SelectionCount)
}
finally {
    $session.Dispose()
}

Write-Host "Adapter integration check passed." -ForegroundColor Green
