param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$CorelDrawDir
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root "DrawPilot.sln"

. (Join-Path $PSScriptRoot "coreldraw-detection.ps1")

$resolvedCorelDrawDir = $CorelDrawDir
if ([string]::IsNullOrWhiteSpace($resolvedCorelDrawDir)) {
    $resolvedCorelDrawDir = $env:CORELDRAW_DIR
}

if ([string]::IsNullOrWhiteSpace($resolvedCorelDrawDir)) {
    $installs = @(Get-CorelDrawInstallations)
    if ($installs.Count -gt 0) {
        $lowest = $installs | Sort-Object Major | Select-Object -First 1
        $resolvedCorelDrawDir = $lowest.InstallDir
        Write-Host ("Using CorelDRAW {0} ({1}) for the interop reference." -f $lowest.VersionDir, $lowest.InstallDir) -ForegroundColor Cyan
    }
    else {
        Write-Host "No CorelDRAW installation detected; relying on Directory.Build.props defaults." -ForegroundColor Yellow
    }
}

$arguments = @($solution, "-c", $Configuration)
if (-not [string]::IsNullOrWhiteSpace($resolvedCorelDrawDir)) {
    $arguments += "-p:CorelDrawDir=$resolvedCorelDrawDir"
}

Write-Host ("Building {0} ({1})..." -f $solution, $Configuration) -ForegroundColor Cyan
dotnet build @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}
