$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$basPath = Join-Path $root "shell\vba\DrawPilot.bas"
$buildDir = Join-Path $root "shell\build"
$gmsPath = Join-Path $buildDir "DrawPilot.gms"
$projectName = "DrawPilot"

if (-not (Get-Process CorelDRW -ErrorAction SilentlyContinue)) {
    throw "CorelDRAW must be running to build the GMS."
}

if (-not (Test-Path $basPath)) {
    throw "VBA source not found: $basPath"
}

New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
if (Test-Path $gmsPath) {
    Remove-Item $gmsPath -Force
}

$app = New-Object -ComObject CorelDRAW.Application
$app.InitializeVBA() | Out-Null

$loaded = @()
foreach ($p in $app.GMSManager.Projects) {
    if ($p.Name -eq $projectName) {
        $loaded += $p
    }
}

foreach ($p in $loaded) {
    if ($p.Dirty) {
        throw "A modified '$projectName' GMS is loaded. Save or close it in CorelDRAW, then retry."
    }
    $p.Unload()
}

$gmsProject = $null
try {
    $gmsProject = $app.GMSManager.Projects.Create($projectName, $false, $gmsPath)

    $vbe = $app.VBE
    $vbProject = $null
    foreach ($p in $vbe.VBProjects) {
        if ($p.Name -eq $projectName) {
            $vbProject = $p
            break
        }
    }

    if ($null -eq $vbProject) {
        throw "VBA project '$projectName' was not found after creation."
    }

    $component = $vbProject.VBComponents.Import($basPath)
    if ($vbe.ActiveVBProject.Name -ne $projectName) {
        $component.Activate()
    }

    $save = $vbe.CommandBars.FindControl(1, 3)
    if ($null -eq $save) {
        throw "VBE Save command was not found."
    }

    $save.Execute()
    Start-Sleep -Milliseconds 500

    if ($gmsProject.Dirty) {
        throw "The GMS project is still dirty after saving."
    }

    $file = Get-Item $gmsPath
    Write-Host ("Built {0} ({1} bytes)" -f $gmsPath, $file.Length) -ForegroundColor Green
}
finally {
    if ($null -ne $gmsProject) {
        try {
            if (-not $gmsProject.Dirty) {
                $gmsProject.Unload()
            }
        }
        catch {
        }
    }
}
