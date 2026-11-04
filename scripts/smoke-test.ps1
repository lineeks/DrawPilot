$ErrorActionPreference = "Stop"

if (-not (Get-Process CorelDRW -ErrorAction SilentlyContinue)) {
    Write-Host "CorelDRAW is not running. Start CorelDRAW first." -ForegroundColor Yellow
    exit 1
}

try {
    $app = [Runtime.InteropServices.Marshal]::GetActiveObject("CorelDRAW.Application")
}
catch {
    # CorelDRAW is not registered in the ROT when started normally.
    # COM activation forwards the request to the running instance.
    $app = New-Object -ComObject CorelDRAW.Application
}

Write-Host "Connected to CorelDRAW" -ForegroundColor Green
Write-Host ("  Version: {0} ({1}.{2})" -f $app.Version, $app.VersionMajor, $app.VersionMinor)
Write-Host ("  Program: {0}" -f $app.ProgramPath)
Write-Host ("  Documents: {0}" -f $app.Documents.Count)

$doc = $null
try {
    $doc = $app.ActiveDocument
}
catch {
}

if ($null -eq $doc) {
    Write-Host "  No active document." -ForegroundColor Yellow
    exit 0
}

Write-Host ("  Active document: {0}" -f $doc.Name)
Write-Host ("  File: {0}" -f $doc.FullFileName)

try {
    Write-Host ("  Pages: {0}" -f $doc.Pages.Count)
}
catch {
    Write-Host "  Pages: unavailable"
}

try {
    Write-Host ("  Layers on active page: {0}" -f $doc.ActivePage.Layers.Count)
}
catch {
    Write-Host "  Layers: unavailable"
}

try {
    Write-Host ("  Selected shapes: {0}" -f $app.ActiveSelectionRange.Count)
}
catch {
    Write-Host "  Selected shapes: 0"
}
