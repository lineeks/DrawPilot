[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$BuildShell,

    [string]$GmsRoot,

    [string]$CorelAppDataRoot = (Join-Path $env:APPDATA "Corel")
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$hostOutput = Join-Path $root "src\DrawPilot.Host\bin\$Configuration\net48"
$gmsSource = Join-Path $root "shell\build\DrawPilot.gms"

. (Join-Path $PSScriptRoot "coreldraw-detection.ps1")

if (-not $WhatIfPreference) {
    & (Join-Path $PSScriptRoot "build.ps1") -Configuration $Configuration

    if ($BuildShell) {
        & (Join-Path $PSScriptRoot "build-shell.ps1")
    }
}

$hostExe = Join-Path $hostOutput "DrawPilot.Host.exe"
if (-not $WhatIfPreference -and -not (Test-Path $hostExe)) {
    throw "Host build output not found: $hostExe"
}

$targets = @()
if (-not [string]::IsNullOrWhiteSpace($GmsRoot)) {
    $targets += [pscustomobject]@{
        DisplayName = "(explicit)"
        GmsRoot = $GmsRoot
        Exists = (Test-Path $GmsRoot)
        Major = 0
        InstallDir = $null
        IsOrphan = $false
    }
}
else {
    $installs = @(Get-CorelDrawInstallations)
    $allRoots = @(Get-CorelDrawUserGmsRoots -CorelAppDataRoot $CorelAppDataRoot -Installations $installs)
    $installedRoots = @($allRoots | Where-Object { -not $_.IsOrphan })

    if ($installedRoots.Count -eq 0) {
        $existingRoots = @($allRoots | Where-Object { $_.Exists })
        if ($existingRoots.Count -eq 0) {
            throw "No installed CorelDRAW found. Use -GmsRoot to deploy to a specific GMS folder."
        }

        Write-Host "No installed CorelDRAW version matched; using existing user GMS folders." -ForegroundColor Yellow
        $targets = $existingRoots
    }
    else {
        $targets = $installedRoots

        foreach ($orphan in @($allRoots | Where-Object { $_.IsOrphan })) {
            Write-Host ("Skipping orphaned profile: {0}" -f $orphan.GmsRoot) -ForegroundColor Yellow
        }
    }
}

foreach ($target in $targets) {
    $installDir = Join-Path $target.GmsRoot "DrawPilot\app"
    $gmsTarget = Join-Path $target.GmsRoot "DrawPilot.gms"

    if (-not $target.Exists) {
        if ($PSCmdlet.ShouldProcess($target.GmsRoot, "Create GMS folder")) {
            New-Item -ItemType Directory -Path $target.GmsRoot -Force | Out-Null
        }
    }

    if ($PSCmdlet.ShouldProcess($target.GmsRoot, "Deploy DrawPilot")) {
        if (Test-Path $installDir) {
            Remove-Item $installDir -Recurse -Force
        }

        New-Item -ItemType Directory -Path $installDir -Force | Out-Null
        Copy-Item (Join-Path $hostOutput "*") $installDir -Recurse -Force
        Write-Host ("Host deployed to {0}" -f $installDir) -ForegroundColor Green

        if (Test-Path $gmsSource) {
            Copy-Item $gmsSource $gmsTarget -Force
            Write-Host ("GMS deployed to {0}" -f $gmsTarget) -ForegroundColor Green
        }
        else {
            Write-Host ("GMS not found at {0}; skipped." -f $gmsSource) -ForegroundColor Yellow
            Write-Host "Follow shell\README.md to create it the first time." -ForegroundColor Yellow
        }
    }

    $oldGms = Join-Path $target.GmsRoot "CorelDRAWTool.gms"
    if (Test-Path $oldGms) {
        Write-Host ("Old deployment detected: {0}. Remove it to avoid duplicate macros." -f $oldGms) -ForegroundColor Yellow
    }
}
