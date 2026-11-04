$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "src\CorelDRAWTool.Host\bin\Release\net48"
$hostExe = Join-Path $output "CorelDRAWTool.Host.exe"
$dataDir = Join-Path (Split-Path $output -Parent) "data"
$settingsPath = Join-Path $dataDir "settings.json"
$logDir = Join-Path $dataDir "logs"
$inputDir = Join-Path $env:TEMP "CorelDRAWTool-batch-ui\input"
$pdfDir = Join-Path $env:TEMP "CorelDRAWTool-batch-ui\output"
$sample = Join-Path $inputDir "Sample.cdr"
$pdf = Join-Path $pdfDir "Sample.pdf"

if (-not (Get-Process CorelDRW -ErrorAction SilentlyContinue)) {
    throw "CorelDRAW is not running."
}

New-Item -ItemType Directory -Path $inputDir -Force | Out-Null

if (-not (Test-Path $sample)) {
    $app = New-Object -ComObject CorelDRAW.Application
    $doc = $app.CreateDocument()
    $doc.ActiveLayer.CreateRectangle(0, 0, 100, 100, 0, 0, 0, 0) | Out-Null
    $doc.SaveAs($sample, $app.CreateStructSaveAsOptions())
    try {
        $doc.Dirty = $false
    }
    catch {
    }
    $doc.Close()
}

if (-not (Test-Path $hostExe)) {
    throw "Host not built: $hostExe"
}

New-Item -ItemType Directory -Path $pdfDir -Force | Out-Null
Get-ChildItem $pdfDir -Filter "*.pdf" -ErrorAction SilentlyContinue | Remove-Item -Force

# Reset settings so the test starts with PDF / all pages / 300 DPI defaults.
New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
if (Test-Path $settingsPath) {
    Remove-Item $settingsPath -Force
}

$beforeLogs = @()
if (Test-Path $logDir) {
    $beforeLogs = @(Get-ChildItem $logDir -Filter "batch-*.log" | Select-Object -ExpandProperty FullName)
}

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
    $startButton = Find-ByAutomationId $window "StartBatchExportButton"
    $formatBox = Find-ByAutomationId $window "FormatBox"
    $pageRangeBox = Find-ByAutomationId $window "PageRangeBox"
    $dpiBox = Find-ByAutomationId $window "DpiBox"

    if ($null -eq $inputBox -or $null -eq $outputBox -or $null -eq $startButton) {
        throw "Batch export controls were not found."
    }

    if ($null -eq $formatBox -or $null -eq $pageRangeBox -or $null -eq $dpiBox) {
        throw "Batch export option controls were not found."
    }

    Set-TextValue $inputBox $inputDir
    Set-TextValue $outputBox $pdfDir
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

    if (-not (Test-Path $pdf)) {
        throw "PDF was not created: $pdf"
    }

    $size = (Get-Item $pdf).Length
    if ($size -le 0) {
        throw "PDF is empty: $pdf"
    }

    if (-not (Test-Path $settingsPath)) {
        throw "Settings file was not saved: $settingsPath"
    }

    $newLogs = @()
    if (Test-Path $logDir) {
        $newLogs = @(Get-ChildItem $logDir -Filter "batch-*.log" |
            Where-Object { $beforeLogs -notcontains $_.FullName })
    }

    if ($newLogs.Count -eq 0) {
        throw "Batch log was not created in $logDir"
    }

    $logText = Get-Content $newLogs[0].FullName -Raw -Encoding UTF8
    if ($logText -notmatch "CorelDRAWTool") {
        throw "Batch log does not contain the expected header."
    }

    Write-Host ("Batch UI export passed: {0} ({1} bytes)" -f $pdf, $size) -ForegroundColor Green
    Write-Host ("Log: {0}" -f $newLogs[0].FullName) -ForegroundColor Green
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
