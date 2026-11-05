# ======================================================================
# Tactical Assault FPV - PowerShell Installer (Windows & Cross-Platform)
# ======================================================================

param (
    [string]$GameDir = ""
)

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "   Tactical Assault FPV - Mod Installer" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

# 1. Detect Game Directory
if (-not $GameDir) {
    if ($env:TAVR_PATH) {
        $GameDir = $env:TAVR_PATH
    } else {
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
}

if (-not $GameDir -or -not (Test-Path $GameDir)) {
    Write-Host "[ERROR] Could not automatically locate 'Tactical Assault VR'." -ForegroundColor Red
    Write-Host "Usage: .\install.ps1 -GameDir 'C:\Path\To\Tactical Assault VR'" -ForegroundColor Yellow
    exit 1
}

Write-Host "Target Game Directory: $GameDir" -ForegroundColor Green

# 2. Check for dotnet SDK
if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    Write-Host "[ERROR] .NET SDK ('dotnet') not found in PATH!" -ForegroundColor Red
    Write-Host "Please install the .NET 6.0 SDK from https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# 3. Build mod
Write-Host "`n[1/3] Building TacticalAssaultFPV.dll (Release)..." -ForegroundColor Cyan
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptDir "src\TacticalAssaultFPV\TacticalAssaultFPV.csproj"

& dotnet build "$projectPath" -c Release "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Build failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

# 4. Deploy DLL
Write-Host "`n[2/3] Deploying mod to Mods directory..." -ForegroundColor Cyan
$modsDir = Join-Path $GameDir "Mods"
if (-not (Test-Path $modsDir)) {
    New-Item -ItemType Directory -Path $modsDir -Force | Out-Null
}

$sourceDll = Join-Path $scriptDir "bin\Release\TacticalAssaultFPV.dll"
$destDll = Join-Path $modsDir "TacticalAssaultFPV.dll"
Copy-Item -Path $sourceDll -Destination $destDll -Force

# 5. Check MelonLoader
Write-Host "`n[3/3] Checking MelonLoader installation..." -ForegroundColor Cyan
$versionDll = Join-Path $GameDir "version.dll"
if (-not (Test-Path $versionDll)) {
    Write-Host "[WARNING] MelonLoader was not detected in '$GameDir'!" -ForegroundColor Yellow
    Write-Host "Please install MelonLoader 0.6.x+ for Tactical Assault VR: https://github.com/LavaGang/MelonLoader.Installer/releases" -ForegroundColor Yellow
} else {
    Write-Host "[OK] MelonLoader detected." -ForegroundColor Green
}

Write-Host "`n======================================================================" -ForegroundColor Cyan
Write-Host "   Installation Succeeded!" -ForegroundColor Green
Write-Host "   TacticalAssaultFPV.dll is installed in: $destDll" -ForegroundColor Green
Write-Host "   Start Tactical Assault VR via Steam to fly your FPV drones!" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Cyan
