$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "src\CorelDRAWTool.Host\bin\Release\net48"
$hostExe = Join-Path $output "CorelDRAWTool.Host.exe"
$dataDir = Join-Path (Split-Path $output -Parent) "data"
$settingsPath = Join-Path $dataDir "settings.json"
$inputDir = Join-Path $env:TEMP "CorelDRAWTool-batch-ui\input-png"
$pngDir = Join-Path $env:TEMP "CorelDRAWTool-batch-ui\output-png"
$sample = Join-Path $inputDir "Sample4.cdr"

if (-not (Get-Process CorelDRW -ErrorAction SilentlyContinue)) {
    throw "CorelDRAW is not running."
}

if (-not (Test-Path $hostExe)) {
    throw "Host not built: $hostExe"
}

New-Item -ItemType Directory -Path $inputDir -Force | Out-Null
New-Item -ItemType Directory -Path $pngDir -Force | Out-Null
Get-ChildItem $pngDir -Filter "*.png" -ErrorAction SilentlyContinue | Remove-Item -Force

if (-not (Test-Path $sample)) {
    $app = New-Object -ComObject CorelDRAW.Application
    $doc = $app.CreateDocument()
    $doc.ActiveLayer.CreateRectangle(1, 1, 5, 5, 0, 0, 0, 0) | Out-Null
    $doc.AddPages(1) | Out-Null
    $doc.Pages.Item(2).Activate()
    $doc.ActiveLayer.CreateRectangle(2, 2, 6, 6, 0, 0, 0, 0) | Out-Null
    $doc.SaveAs($sample, $app.CreateStructSaveAsOptions())
    try { $doc.Dirty = $false } catch { }
    $doc.Close()
}

# Start from a PNG / all pages / 150 DPI settings file so the test does not
# depend on ComboBox automation; the Host must load and display these settings.
New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
if (Test-Path $settingsPath) {
    Remove-Item $settingsPath -Force
}

$coreDll = Join-Path $output "CorelDRAWTool.Core.dll"
[Reflection.Assembly]::LoadFrom($coreDll) | Out-Null

$settings = New-Object -TypeName CorelDRAWTool.Core.Settings.BatchExportSettings
$settings.InputFolder = $inputDir
$settings.OutputFolder = $pngDir
$settings.Format = [CorelDRAWTool.Core.Models.BatchExportFormat]::Png
$settings.PageRange = [CorelDRAWTool.Core.Models.BatchExportPageRange]::AllPages
$settings.ImageDpi = 150
$settings.OverwriteExisting = $false
[CorelDRAWTool.Core.Settings.BatchExportSettingsStore]::Save($settingsPath, $settings)

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

function Find-ByAutomationId {
    param($Window, [string]$AutomationId)

    $condition = New-Object -TypeName System.Windows.Automation.PropertyCondition -ArgumentList @(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $AutomationId
    )
    return $Window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Set-TextValue {
    param($Element, [string]$Value)

    $pattern = $Element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $pattern.SetValue($Value)
}

function Invoke-Button {
    param($Element)

    $pattern = $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}

$process = Start-Process -FilePath $hostExe -ArgumentList "--command=batch-export" -PassThru

try {
    $window = $null
    for ($i = 0; $i -lt 30 -and $null -eq $window; $i++) {
        Start-Sleep -Milliseconds 500
        if ($process.HasExited) {
            throw "Host exited with code $($process.ExitCode)."
        }

        $condition = New-Object -TypeName System.Windows.Automation.PropertyCondition -ArgumentList @(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
            $process.Id
        )
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Children,
            $condition)
    }

    if ($null -eq $window) {
        throw "Host window did not appear."
    }

    $inputBox = Find-ByAutomationId $window "InputFolderBox"
    $outputBox = Find-ByAutomationId $window "OutputFolderBox"
    $formatBox = Find-ByAutomationId $window "FormatBox"
    $startButton = Find-ByAutomationId $window "StartBatchExportButton"

    if ($null -eq $inputBox -or $null -eq $outputBox -or $null -eq $formatBox -or $null -eq $startButton) {
        throw "Batch export controls were not found."
    }

    try {
        $selectionPattern = $formatBox.GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern)
        $selectedItems = $selectionPattern.Current.GetSelection()
        if ($selectedItems.Length -gt 0) {
            Write-Host ("Format selected: {0}" -f $selectedItems[0].Current.Name) -ForegroundColor Green
        }
    }
    catch {
    }

    Set-TextValue $inputBox $inputDir
    Set-TextValue $outputBox $pngDir
    Start-Sleep -Milliseconds 300
    Invoke-Button $startButton

    $deadline = (Get-Date).AddSeconds(120)
    $finished = $false
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
        if ($process.HasExited) {
            throw "Host exited while exporting."
        }

        if ($startButton.Current.IsEnabled) {
            $finished = $true
            break
        }
    }

    if (-not $finished) {
        throw "Batch export did not finish within 120 seconds."
    }

    $png1 = Join-Path $pngDir "Sample4-p01.png"
    $png2 = Join-Path $pngDir "Sample4-p02.png"

    if (-not (Test-Path $png1) -or -not (Test-Path $png2)) {
        throw "Expected PNG outputs were not created."
    }

    Write-Host ("Batch UI PNG export passed: {0} bytes, {1} bytes" -f (Get-Item $png1).Length, (Get-Item $png2).Length) -ForegroundColor Green
}
finally {
    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        Start-Sleep -Seconds 2
    }

    if (-not $process.HasExited) {
        $process.Kill()
    }
}
