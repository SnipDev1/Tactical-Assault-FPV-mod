@echo off
setlocal enabledelayedexpansion

echo ======================================================================
echo    Tactical Assault FPV - Windows Mod Installer
echo ======================================================================
echo.

:: 1. Detect or use user-provided Game Directory
set "GAME_DIR=%~1"

if "%GAME_DIR%"=="" (
    if defined TAVR_PATH (
        set "GAME_DIR=%TAVR_PATH%"
    )
)

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
    echo.
    echo Usage:
    echo   install.bat "C:\Path\To\Tactical Assault VR"
    echo.
    pause
    exit /b 1
)

echo Target Game Directory: "%GAME_DIR%"
if not exist "%GAME_DIR%" (
    echo [ERROR] Game directory does not exist: "%GAME_DIR%"
    pause
    exit /b 1
)

:: 2. Check for dotnet SDK
where dotnet >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo [ERROR] .NET SDK ('dotnet') was not found in your PATH!
    echo Please install the .NET 6.0 SDK from:
    echo https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

:: 3. Build mod
echo.
echo [1/3] Building TacticalAssaultFPV.dll (Release)...
dotnet build "%~dp0src\TacticalAssaultFPV\TacticalAssaultFPV.csproj" -c Release -p:GameDir="%GAME_DIR%"
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Build failed! Check compiler output above.
    pause
    exit /b %ERRORLEVEL%
)

:: 4. Deploy DLL
echo.
echo [2/3] Deploying mod to Mods directory...
if not exist "%GAME_DIR%\Mods" (
    mkdir "%GAME_DIR%\Mods"
)

copy /Y "%~dp0bin\Release\TacticalAssaultFPV.dll" "%GAME_DIR%\Mods\TacticalAssaultFPV.dll" >nul
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Failed to copy TacticalAssaultFPV.dll to "%GAME_DIR%\Mods\".
    pause
    exit /b 1
)

:: 5. Check MelonLoader installation
echo.
echo [3/3] Checking MelonLoader installation...
if not exist "%GAME_DIR%\version.dll" (
    echo [WARNING] MelonLoader was not detected in "%GAME_DIR%"!
    echo Please install MelonLoader 0.6.x+ for Tactical Assault VR:
    echo https://github.com/LavaGang/MelonLoader.Installer/releases
) else (
    echo [OK] MelonLoader detected.
)

echo.
echo ======================================================================
echo    Installation Succeeded!
echo    TacticalAssaultFPV.dll is installed in "%GAME_DIR%\Mods\"
echo    Start Tactical Assault VR via Steam to fly your FPV drones!
echo ======================================================================
echo.
pause
