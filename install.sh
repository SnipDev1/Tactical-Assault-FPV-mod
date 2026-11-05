#!/usr/bin/env bash
set -e

GAME_DIR="/mnt/olddisk/SteamLibrary/steamapps/common/Tactical Assault VR"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "=== Installing TacticalAssaultFPV Mod into Tactical Assault VR ==="
echo "Game directory: $GAME_DIR"

# 1. Build mod
echo "[1/4] Building mod DLL..."
cd "$SCRIPT_DIR/src/TacticalAssaultFPV"
dotnet build -c Release

# 2. Install MelonLoader core
echo "[2/4] Installing MelonLoader core files..."
cp -u "$SCRIPT_DIR/tools/melonloader/version.dll" "$GAME_DIR/"
mkdir -p "$GAME_DIR/MelonLoader"
cp -ru "$SCRIPT_DIR/tools/melonloader/MelonLoader/"* "$GAME_DIR/MelonLoader/"

# 3. Pre-seed generated Il2CppAssemblies to accelerate initial game launch
echo "[3/4] Pre-seeding Il2CppAssemblies..."
mkdir -p "$GAME_DIR/MelonLoader/Il2CppAssemblies"
cp -ru "$SCRIPT_DIR/interop/"*.dll "$GAME_DIR/MelonLoader/Il2CppAssemblies/" 2>/dev/null || true

# 4. Copy mod DLL to Mods folder
echo "[4/4] Installing TacticalAssaultFPV.dll into Mods/..."
mkdir -p "$GAME_DIR/Mods"
cp "$SCRIPT_DIR/bin/Release/TacticalAssaultFPV.dll" "$GAME_DIR/Mods/"

echo "=========================================================="
echo "Installation complete!"
echo ""
echo "IMPORTANT FOR LINUX / PROTON USERS:"
echo "In your Steam Library, right click 'Tactical Assault VR' -> Properties -> Launch Options:"
echo "Set the launch options to:"
echo 'WINEDLLOVERRIDES="version=n,b" %command%'
echo "=========================================================="
