# Shared CorelDRAW discovery helpers for build.ps1 and deploy.ps1.
# Keep this file ASCII-only: Windows PowerShell 5.1 may read BOM-less UTF-8 as ANSI.

function Get-CorelDrawRegistryProducts {
    $products = @()
    $roots = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    )

    foreach ($root in $roots) {
        if (-not (Test-Path $root)) {
            continue
        }

        Get-ChildItem $root -ErrorAction SilentlyContinue | ForEach-Object {
            $item = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
            if ($null -eq $item) {
                return
            }

            $displayName = [string]$item.DisplayName
            if ($displayName -notmatch '^CorelDRAW .+ Suite \d{4}$') {
                return
            }

            $location = [string]$item.InstallLocation
            if ([string]::IsNullOrWhiteSpace($location)) {
                return
            }

            $products += [pscustomobject]@{
                DisplayName = $displayName
                DisplayVersion = [string]$item.DisplayVersion
                InstallDir = $location.TrimEnd('\')
            }
        }
    }

    return $products
}

function Get-CorelDrawInstallations {
    param(
        [string]$ProgramFilesRoot = (Join-Path $env:ProgramFiles "Corel")
    )

    if (-not (Test-Path $ProgramFilesRoot)) {
        return @()
    }

    $products = @(Get-CorelDrawRegistryProducts)
    $installs = @()

    Get-ChildItem $ProgramFilesRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        $suite = $_

        Get-ChildItem $suite.FullName -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            $versionDir = $_
            $interopPath = Join-Path $versionDir.FullName "Programs64\Assemblies\Corel.Interop.VGCore.dll"
            if (-not (Test-Path $interopPath)) {
                return
            }

            $major = 0
            if (-not [int]::TryParse($versionDir.Name, [ref]$major)) {
                return
            }

            $match = $products |
                Where-Object { $_.InstallDir -ieq $versionDir.FullName.TrimEnd('\') } |
                Select-Object -First 1

            $displayName = $null
            $displayVersion = $null
            $year = $null
            if ($null -ne $match) {
                $displayName = $match.DisplayName
                $displayVersion = $match.DisplayVersion
                if ($displayName -match '(\d{4})$') {
                    $year = $matches[1]
                }
            }

            $installs += [pscustomobject]@{
                SuiteName = $suite.Name
                VersionDir = $versionDir.Name
                Major = $major
                InstallDir = $versionDir.FullName
                InteropPath = $interopPath
                DisplayName = $displayName
                DisplayVersion = $displayVersion
                Year = $year
            }
        }
    }

    return $installs
}

function Get-CorelDrawUserGmsRoots {
    param(
        [string]$CorelAppDataRoot = (Join-Path $env:APPDATA "Corel"),
        [object[]]$Installations = @()
    )

    $roots = @()
    $matchedNames = @{}

    foreach ($install in $Installations) {
        $name = $install.DisplayName
        if ([string]::IsNullOrWhiteSpace($name)) {
            continue
        }

        $gmsRoot = Join-Path $CorelAppDataRoot (Join-Path $name "Draw\GMS")
        $roots += [pscustomobject]@{
            DisplayName = $name
            GmsRoot = $gmsRoot
            Exists = (Test-Path $gmsRoot)
            Major = $install.Major
            InstallDir = $install.InstallDir
            IsOrphan = $false
        }

        $matchedNames[$name.ToLowerInvariant()] = $true
    }

    if (Test-Path $CorelAppDataRoot) {
        Get-ChildItem $CorelAppDataRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            if ($_.Name -notmatch '^CorelDRAW .+ Suite \d{4}$') {
                return
            }

            if ($matchedNames.ContainsKey($_.Name.ToLowerInvariant())) {
                return
            }

            $gmsRoot = Join-Path $_.FullName "Draw\GMS"
            $roots += [pscustomobject]@{
                DisplayName = $_.Name
                GmsRoot = $gmsRoot
                Exists = (Test-Path $gmsRoot)
                Major = 0
                InstallDir = $null
                IsOrphan = $true
            }
        }
    }

    return $roots
}
