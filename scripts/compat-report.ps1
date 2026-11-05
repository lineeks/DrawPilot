param(
    [string]$AppDir,
    [string]$OutputPath
)

$ErrorActionPreference = "Continue"

. (Join-Path $PSScriptRoot "coreldraw-detection.ps1")

$script:lines = @()

function Add-Line {
    param([string]$Text)
    $script:lines += $Text
    Write-Host $Text
}

function Add-Probe {
    param([string]$Name, [scriptblock]$Action)

    try {
        $result = & $Action
        if ($null -eq $result) {
            Add-Line ("    {0}: OK" -f $Name)
        }
        else {
            Add-Line ("    {0}: OK ({1})" -f $Name, $result)
        }
    }
    catch {
        Add-Line ("    {0}: FAIL - {1}" -f $Name, $_.Exception.Message)
    }
}

function Test-ComMember {
    param([object]$Object, [string]$Name)

    if ($null -eq $Object) {
        return $false
    }

    try {
        return ($null -ne ($Object | Get-Member -Name $Name -ErrorAction SilentlyContinue))
    }
    catch {
        return $false
    }
}

Add-Line "DrawPilot compatibility report"
Add-Line ("Generated: {0}" -f (Get-Date).ToString("yyyy-MM-dd HH:mm:ss"))
Add-Line ""

Add-Line "== Installed CorelDRAW =="
$installs = @(Get-CorelDrawInstallations)
if ($installs.Count -eq 0) {
    Add-Line "  (none detected)"
}
else {
    foreach ($install in $installs) {
        Add-Line ("  {0} | major {1} | {2}" -f $install.DisplayName, $install.Major, $install.InstallDir)
        Add-Line ("    interop: {0}" -f $install.InteropPath)
    }
}

Add-Line ""
Add-Line "== User GMS folders =="
$roots = @(Get-CorelDrawUserGmsRoots -Installations $installs)
if ($roots.Count -eq 0) {
    Add-Line "  (none detected)"
}
else {
    foreach ($root in $roots) {
        $tag = "installed"
        if ($root.IsOrphan) {
            $tag = "orphan"
        }

        Add-Line ("  [{0}] {1} (exists: {2})" -f $tag, $root.GmsRoot, $root.Exists)
    }
}

Add-Line ""
Add-Line "== Running CorelDRAW =="
$process = Get-Process CorelDRW -ErrorAction SilentlyContinue
if ($null -eq $process) {
    Add-Line "  not running"
}
else {
    Add-Line ("  process id: {0}" -f $process.Id)
    try {
        $app = New-Object -ComObject CorelDRAW.Application
        Add-Line ("  version: {0} (major {1}, minor {2})" -f $app.Version, $app.VersionMajor, $app.VersionMinor)
        Add-Line ("  program: {0}" -f $app.ProgramPath)

        $doc = $null
        try {
            $doc = $app.ActiveDocument
        }
        catch {
        }

        if ($null -eq $doc) {
            Add-Line "  document: (none)"
        }
        else {
            Add-Line ("  document: {0}" -f $doc.FullFileName)
            Add-Line ("  pages: {0}" -f $doc.Pages.Count)
        }

        Add-Line "  capability probes:"
        Add-Probe "Application.Version" { $app.Version }
        Add-Probe "Application.GMSManager" {
            if (Test-ComMember $app "GMSManager") { "member present" } else { throw "member missing" }
        }
        Add-Probe "Application.OpenDocumentAsCopy" {
            if (Test-ComMember $app "OpenDocumentAsCopy") { "member present" } else { throw "member missing" }
        }
        Add-Probe "Application.CreateStructOpenOptions" {
            $null = $app.CreateStructOpenOptions()
            "callable"
        }
        Add-Probe "Document.PDFSettings" {
            if ($null -eq $doc) { throw "no active document" }
            $null = $doc.PDFSettings
            "ok"
        }
        Add-Probe "Document.PDFSettings.PublishRange" {
            if ($null -eq $doc) { throw "no active document" }
            $null = $doc.PDFSettings.PublishRange
            "ok"
        }
        Add-Probe "Document.ExportBitmap" {
            if ($null -eq $doc) { throw "no active document" }
            if (Test-ComMember $doc "ExportBitmap") { "member present" } else { throw "member missing" }
        }
        Add-Probe "Document.PublishToPDF" {
            if ($null -eq $doc) { throw "no active document" }
            if (Test-ComMember $doc "PublishToPDF") { "member present" } else { throw "member missing" }
        }
    }
    catch {
        Add-Line ("  COM error: {0}" -f $_.Exception.Message)
    }
}

Add-Line ""
Add-Line "== DrawPilot adapter =="
if ([string]::IsNullOrWhiteSpace($AppDir)) {
    if (Test-Path (Join-Path $PSScriptRoot "DrawPilot.Corel.dll")) {
        $AppDir = $PSScriptRoot
    }
    else {
        $AppDir = Join-Path (Split-Path -Parent $PSScriptRoot) "src\DrawPilot.Host\bin\Release\net48"
    }
}

$corelDll = Join-Path $AppDir "DrawPilot.Corel.dll"
if (-not (Test-Path $corelDll)) {
    Add-Line ("  assemblies not found at {0}; skipped" -f $AppDir)
}
else {
    foreach ($name in @("Corel.Interop.VGCore.dll", "DrawPilot.Core.dll", "DrawPilot.Corel.dll")) {
        $path = Join-Path $AppDir $name
        if (Test-Path $path) {
            [Reflection.Assembly]::LoadFrom($path) | Out-Null
        }
    }

    try {
        $session = [DrawPilot.Corel.CorelDrawSession]::Attach([TimeSpan]::FromSeconds(5))
        try {
            $application = $session.GetApplicationSummary()
            Add-Line ("  attach: OK ({0})" -f $application.DisplayVersion)
            $document = $session.GetActiveDocumentSummary()
            Add-Line ("  document: {0}" -f $document.DisplayName)
        }
        finally {
            $session.Dispose()
        }
    }
    catch {
        Add-Line ("  attach: FAIL - {0}" -f $_.Exception.Message)
    }
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $dataCandidate = Join-Path (Split-Path -Parent $AppDir) "data"
    if (Test-Path $dataCandidate) {
        $outputDir = Join-Path $dataCandidate "logs"
    }
    else {
        $outputDir = $env:TEMP
    }

    $OutputPath = Join-Path $outputDir ("compat-" + (Get-Date).ToString("yyyyMMdd-HHmmss") + ".txt")
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$encoding = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($OutputPath, ($script:lines -join [Environment]::NewLine), $encoding)
Write-Host ("Report written to {0}" -f $OutputPath) -ForegroundColor Green
