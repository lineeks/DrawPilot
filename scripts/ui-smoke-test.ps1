$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "src\CorelDRAWTool.Host\bin\Release\net48"
$hostExe = Join-Path $output "CorelDRAWTool.Host.exe"

if (-not (Get-Process CorelDRW -ErrorAction SilentlyContinue)) {
    Write-Host "CorelDRAW is not running." -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Path $hostExe)) {
    throw "Host not built: $hostExe. Run scripts\build.ps1 first."
}

foreach ($name in @("Corel.Interop.VGCore.dll", "CorelDRAWTool.Core.dll", "CorelDRAWTool.Corel.dll")) {
    [Reflection.Assembly]::LoadFrom((Join-Path $output $name)) | Out-Null
}

$session = [CorelDRAWTool.Corel.CorelDrawSession]::Attach([TimeSpan]::FromSeconds(5))
try {
    $expectedDoc = $session.GetActiveDocumentSummary()
}
finally {
    $session.Dispose()
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$process = Start-Process -FilePath $hostExe -PassThru
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

    $names = New-Object System.Collections.Generic.List[string]
    $elements = $window.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($element in $elements) {
        if (-not [string]::IsNullOrWhiteSpace($element.Current.Name)) {
            $names.Add($element.Current.Name)
        }
    }

    $expectedTitle = [CorelDRAWTool.Core.Features.FeatureCatalog]::Default.Title
    $checks = @(
        @{ Name = "window title"; Ok = ($window.Current.Name -eq "CorelDRAWTool") },
        @{ Name = "feature title"; Ok = $names.Contains($expectedTitle) },
        @{ Name = "page count"; Ok = $names.Contains($expectedDoc.PageCount.ToString()) },
        @{ Name = "layer count"; Ok = $names.Contains($expectedDoc.LayerCount.ToString()) },
        @{ Name = "selection count"; Ok = $names.Contains($expectedDoc.SelectionCount.ToString()) }
    )

    $failed = $false
    foreach ($check in $checks) {
        if ($check.Ok) {
            Write-Host ("PASS: {0}" -f $check.Name) -ForegroundColor Green
        }
        else {
            Write-Host ("FAIL: {0}" -f $check.Name) -ForegroundColor Red
            $failed = $true
        }
    }

    if ($failed) {
        throw "UI smoke test failed."
    }
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

Write-Host "UI smoke test passed." -ForegroundColor Green
