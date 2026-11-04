$ErrorActionPreference = "Stop"

$gmsDir = Join-Path $env:APPDATA "Corel\CorelDRAW Technical Suite 2025\Draw\GMS"
$gmsPath = Join-Path $gmsDir "CorelDRAWTool.gms"
$appDir = Join-Path $gmsDir "CorelDRAWTool\app"

if (-not (Get-Process CorelDRW -ErrorAction SilentlyContinue)) {
    throw "CorelDRAW is not running."
}

if (-not (Test-Path $gmsPath)) {
    throw "GMS not deployed: $gmsPath"
}

[Reflection.Assembly]::LoadFrom((Join-Path $appDir "CorelDRAWTool.Core.dll")) | Out-Null
$expectedTitle = [CorelDRAWTool.Core.Features.FeatureCatalog]::Find("batch-export").Title

$app = New-Object -ComObject CorelDRAW.Application
$app.InitializeVBA() | Out-Null

$project = $null
$projects = $app.GMSManager.Projects
for ($i = 1; $i -le $projects.Count; $i++) {
    $candidate = $projects.Item($i)
    if ($candidate.Name -eq "CorelDRAWTool") {
        $project = $candidate
    }
}

if ($null -eq $project) {
    $project = $app.GMSManager.Projects.Load($gmsPath, $false, $false)
}

$macro = $null
for ($i = 1; $i -le $project.Macros.Count; $i++) {
    $candidate = $project.Macros.Item($i)
    if ($candidate.Name -like "*ShowBatchExport") {
        $macro = $candidate
    }
}

if ($null -eq $macro) {
    throw "Macro ShowBatchExport was not found in the GMS."
}

$before = @(Get-Process CorelDRAWTool.Host -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
$macro.Run()

$hostProcess = $null
for ($i = 0; $i -lt 30 -and $null -eq $hostProcess; $i++) {
    Start-Sleep -Milliseconds 500
    $candidates = @(Get-Process CorelDRAWTool.Host -ErrorAction SilentlyContinue |
        Where-Object { $before -notcontains $_.Id })
    if ($candidates.Count -gt 0) {
        $hostProcess = $candidates[0]
    }
}

if ($null -eq $hostProcess) {
    throw "Host process did not start from the ShowBatchExport macro."
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$window = $null
for ($i = 0; $i -lt 30 -and $null -eq $window; $i++) {
    Start-Sleep -Milliseconds 500
    if ($hostProcess.HasExited) {
        throw "Host exited with code $($hostProcess.ExitCode)."
    }

    $condition = New-Object -TypeName System.Windows.Automation.PropertyCondition -ArgumentList @(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $hostProcess.Id
    )
    $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
        [System.Windows.Automation.TreeScope]::Children,
        $condition)
}

if ($null -eq $window) {
    throw "Host window did not appear."
}

$names = New-Object System.Collections.Generic.List[string]
$elements = $window.FindAll(
    [System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.Condition]::TrueCondition)
foreach ($element in $elements) {
    if (-not [string]::IsNullOrWhiteSpace($element.Current.Name)) {
        $names.Add($element.Current.Name)
    }
}

if (-not $names.Contains($expectedTitle)) {
    throw "Batch export view was not shown."
}

$formatCondition = New-Object -TypeName System.Windows.Automation.PropertyCondition -ArgumentList @(
    [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
    "FormatBox"
)
$formatBox = $window.FindFirst(
    [System.Windows.Automation.TreeScope]::Descendants,
    $formatCondition)

if ($null -eq $formatBox) {
    throw "Batch export format selector was not found."
}

Write-Host ("Host window: {0}" -f $window.Current.Name) -ForegroundColor Green
Write-Host ("Batch feature found: {0}" -f $expectedTitle) -ForegroundColor Green
Write-Host ("Host PID: {0}" -f $hostProcess.Id) -ForegroundColor Green

$hostProcess.CloseMainWindow() | Out-Null
Start-Sleep -Seconds 2
if (-not $hostProcess.HasExited) {
    $hostProcess.Kill()
}

Write-Host "GMS ShowBatchExport end-to-end check passed." -ForegroundColor Green
