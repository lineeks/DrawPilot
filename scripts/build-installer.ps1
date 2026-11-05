param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$BuildShell,

    [string]$Version
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root "dist"
$stage = Join-Path $dist "installer-stage"
$hostOutput = Join-Path $root "src\DrawPilot.Host\bin\$Configuration\net48"
$gmsSource = Join-Path $root "shell\build\DrawPilot.gms"
$issPath = Join-Path $root "installer\DrawPilot.iss"

function Get-InnoCompiler {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 7\ISCC.exe")
    )

    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $candidates += (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
        $candidates += (Join-Path $env:ProgramFiles "Inno Setup 7\ISCC.exe")
    }

    $programFilesX86 = ${env:ProgramFiles(x86)}
    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        $candidates += (Join-Path $programFilesX86 "Inno Setup 6\ISCC.exe")
        $candidates += (Join-Path $programFilesX86 "Inno Setup 7\ISCC.exe")
    }

    foreach ($path in $candidates) {
        if (Test-Path $path) {
            return $path
        }
    }

    throw "Inno Setup compiler (ISCC.exe) not found. Install Inno Setup 6."
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $versionMatch = Select-String -Path (Join-Path $root "Directory.Build.props") -Pattern '<Version>([^<]+)</Version>'
    if ($null -eq $versionMatch) {
        throw "Could not read Version from Directory.Build.props."
    }

    $Version = $versionMatch.Matches[0].Groups[1].Value
}

& (Join-Path $PSScriptRoot "build.ps1") -Configuration $Configuration

if ($BuildShell) {
    & (Join-Path $PSScriptRoot "build-shell.ps1")
}

if (-not (Test-Path $gmsSource)) {
    throw "GMS not found at $gmsSource. Run build-shell.ps1 with CorelDRAW running, or pass -BuildShell."
}

if (Test-Path $stage) {
    Remove-Item $stage -Recurse -Force
}

$stageApp = Join-Path $stage "app"
New-Item -ItemType Directory -Path $stageApp -Force | Out-Null
Copy-Item (Join-Path $hostOutput "*") $stageApp -Recurse -Force
Get-ChildItem $stageApp -Filter "*.pdb" -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force
Copy-Item $gmsSource (Join-Path $stage "DrawPilot.gms") -Force

New-Item -ItemType Directory -Path $dist -Force | Out-Null

$iscc = Get-InnoCompiler
Write-Host ("Compiling installer with {0}" -f $iscc) -ForegroundColor Cyan
& $iscc "/DAppVersion=$Version" "/DStageDir=$stage" "/O$dist" $issPath
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

$installer = Join-Path $dist ("DrawPilot-Setup-" + $Version + ".exe")
if (-not (Test-Path $installer)) {
    throw "Installer output not found: $installer"
}

Write-Host ("Installer created: {0}" -f $installer) -ForegroundColor Green
