$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "src\DrawPilot.Corel\bin\Release\net48"
$tempRoot = Join-Path $env:TEMP "DrawPilot-adapter-test"
$inputDir = Join-Path $tempRoot "input"
$outRoot = Join-Path $tempRoot "output"
$sample4 = Join-Path $inputDir "Sample4.cdr"
$sampleBlank = Join-Path $inputDir "SampleBlank.cdr"

if (-not (Get-Process CorelDRW -ErrorAction SilentlyContinue)) {
    throw "CorelDRAW is not running."
}

New-Item -ItemType Directory -Path $inputDir -Force | Out-Null
New-Item -ItemType Directory -Path $outRoot -Force | Out-Null

function New-TwoPageSample {
    param(
        [string]$Path,
        [bool]$BlankFirstPage
    )

    $app = New-Object -ComObject CorelDRAW.Application
    $doc = $app.CreateDocument()

    if (-not $BlankFirstPage) {
        $doc.ActiveLayer.CreateRectangle(1, 1, 5, 5, 0, 0, 0, 0) | Out-Null
    }

    $doc.AddPages(1) | Out-Null
    $doc.Pages.Item(2).Activate()
    $doc.ActiveLayer.CreateRectangle(2, 2, 6, 6, 0, 0, 0, 0) | Out-Null
    $doc.SaveAs($Path, $app.CreateStructSaveAsOptions())
    try { $doc.Dirty = $false } catch { }
    $doc.Close()
}

if (-not (Test-Path $sample4)) {
    New-TwoPageSample -Path $sample4 -BlankFirstPage $false
}

if (-not (Test-Path $sampleBlank)) {
    New-TwoPageSample -Path $sampleBlank -BlankFirstPage $true
}

$coreDll = Join-Path $output "DrawPilot.Core.dll"
$corelDll = Join-Path $output "DrawPilot.Corel.dll"
$interopDll = Join-Path $output "Corel.Interop.VGCore.dll"

if (-not (Test-Path $coreDll)) {
    throw "Build output not found: $coreDll"
}

[Reflection.Assembly]::LoadFrom($interopDll) | Out-Null
[Reflection.Assembly]::LoadFrom($coreDll) | Out-Null
[Reflection.Assembly]::LoadFrom($corelDll) | Out-Null

$session = [DrawPilot.Corel.CorelDrawSession]::Attach([TimeSpan]::FromSeconds(5))

function Test-Export {
    param(
        [string]$Name,
        [string]$Source,
        [DrawPilot.Core.Models.BatchExportFormat]$Format,
        [DrawPilot.Core.Models.BatchExportPageRange]$PageRange,
        [int]$Dpi,
        [string]$OutputFileName
    )

    $outDir = Join-Path $outRoot $Name
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
    Get-ChildItem $outDir -File -ErrorAction SilentlyContinue | Remove-Item -Force

    $request = New-Object -TypeName DrawPilot.Core.Models.BatchExportRequest -ArgumentList @(
        (Split-Path $Source -Parent),
        $outDir,
        $Format,
        $PageRange,
        $Dpi,
        $false
    )

    $item = New-Object -TypeName DrawPilot.Core.Models.BatchExportItem -ArgumentList @(
        $Source,
        (Join-Path $outDir $OutputFileName),
        $false
    )

    $result = $session.ExportDocument($item, $request)
    Write-Host ("{0}: outputs={1} warnings={2}" -f $Name, $result.OutputPaths.Count, $result.Warnings.Count) -ForegroundColor Green
    foreach ($path in $result.OutputPaths) {
        Write-Host ("  " + $path)
    }
    foreach ($warning in $result.Warnings) {
        Write-Host ("  warning: " + $warning) -ForegroundColor Yellow
    }
}

try {
    Test-Export -Name "png-all" -Source $sample4 -Format ([DrawPilot.Core.Models.BatchExportFormat]::Png) -PageRange ([DrawPilot.Core.Models.BatchExportPageRange]::AllPages) -Dpi 150 -OutputFileName "Sample4-p01.png"
    Test-Export -Name "tiff-all" -Source $sample4 -Format ([DrawPilot.Core.Models.BatchExportFormat]::Tiff) -PageRange ([DrawPilot.Core.Models.BatchExportPageRange]::AllPages) -Dpi 150 -OutputFileName "Sample4-p01.tif"
    Test-Export -Name "pdf-all" -Source $sample4 -Format ([DrawPilot.Core.Models.BatchExportFormat]::Pdf) -PageRange ([DrawPilot.Core.Models.BatchExportPageRange]::AllPages) -Dpi 300 -OutputFileName "Sample4.pdf"
    Test-Export -Name "pdf-current" -Source $sample4 -Format ([DrawPilot.Core.Models.BatchExportFormat]::Pdf) -PageRange ([DrawPilot.Core.Models.BatchExportPageRange]::CurrentPage) -Dpi 300 -OutputFileName "Sample4-current.pdf"
    Test-Export -Name "png-blank-page" -Source $sampleBlank -Format ([DrawPilot.Core.Models.BatchExportFormat]::Png) -PageRange ([DrawPilot.Core.Models.BatchExportPageRange]::AllPages) -Dpi 150 -OutputFileName "SampleBlank-p01.png"
}
finally {
    $session.Dispose()
}

Write-Host "Adapter export checks completed." -ForegroundColor Green
