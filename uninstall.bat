@echo off
setlocal enabledelayedexpansion

echo ======================================================================
echo    Tactical Assault FPV - Windows Mod Uninstaller
echo ======================================================================
echo.

set "GAME_DIR=%~1"

if "%GAME_DIR%"=="" (
    if exist "C:\Program Files (x86)\Steam\steamapps\common\Tactical Assault VR" (
        set "GAME_DIR=C:\Program Files (x86)\Steam\steamapps\common\Tactical Assault VR"
    ) else if exist "C:\SteamLibrary\steamapps\common\Tactical Assault VR" (
        set "GAME_DIR=C:\SteamLibrary\steamapps\common\Tactical Assault VR"
    ) else if exist "D:\SteamLibrary\steamapps\common\Tactical Assault VR" (
        set "GAME_DIR=D:\SteamLibrary\steamapps\common\Tactical Assault VR"
    ) else if exist "E:\SteamLibrary\steamapps\common\Tactical Assault VR" (
        set "GAME_DIR=E:\SteamLibrary\steamapps\common\Tactical Assault VR"
    )
)

if "%GAME_DIR%"=="" (
    echo [ERROR] Could not automatically locate 'Tactical Assault VR'.
    echo Usage:
    echo   uninstall.bat "C:\Path\To\Tactical Assault VR"
    pause
    exit /b 1
)

echo Target Game Directory: "%GAME_DIR%"

if exist "%GAME_DIR%\Mods\TacticalAssaultFPV.dll" (
    del /F /Q "%GAME_DIR%\Mods\TacticalAssaultFPV.dll"
    echo [OK] Removed TacticalAssaultFPV.dll from Mods folder.
) else (
    echo [INFO] TacticalAssaultFPV.dll was not found in Mods folder.
)

echo.
echo ======================================================================
echo    Uninstallation Complete!
echo ======================================================================
echo.
pause
