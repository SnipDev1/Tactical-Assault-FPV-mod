# ======================================================================
# Tactical Assault FPV - PowerShell Uninstaller
# ======================================================================

param (
    [string]$GameDir = ""
)

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "   Tactical Assault FPV - Mod Uninstaller" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

if (-not $GameDir) {
    $commonPaths = @(
        "C:\Program Files (x86)\Steam\steamapps\common\Tactical Assault VR",
        "C:\SteamLibrary\steamapps\common\Tactical Assault VR",
        "D:\SteamLibrary\steamapps\common\Tactical Assault VR",
        "E:\SteamLibrary\steamapps\common\Tactical Assault VR"
    )
    foreach ($path in $commonPaths) {
        if (Test-Path $path) {
            $GameDir = $path
            break
        }
    }
}

if (-not $GameDir -or -not (Test-Path $GameDir)) {
    Write-Host "[ERROR] Could not automatically locate 'Tactical Assault VR'." -ForegroundColor Red
    Write-Host "Usage: .\uninstall.ps1 -GameDir 'C:\Path\To\Tactical Assault VR'" -ForegroundColor Yellow
    exit 1
}

$targetDll = Join-Path $GameDir "Mods\TacticalAssaultFPV.dll"
if (Test-Path $targetDll) {
    Remove-Item -Path $targetDll -Force
    Write-Host "[OK] Removed TacticalAssaultFPV.dll from Mods folder." -ForegroundColor Green
} else {
    Write-Host "[INFO] TacticalAssaultFPV.dll was not found in Mods folder." -ForegroundColor Gray
}

Write-Host "`nUninstallation Complete!" -ForegroundColor Green
