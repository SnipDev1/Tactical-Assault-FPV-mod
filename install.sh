#!/usr/bin/env bash
set -e

# Default game directory if not specified via environment variable or argument
DEFAULT_GAME_DIR="/mnt/olddisk/SteamLibrary/steamapps/common/Tactical Assault VR"
GAME_DIR="${1:-${GAME_DIR:-$DEFAULT_GAME_DIR}}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "=== Installing TacticalAssaultFPV Mod into Tactical Assault VR ==="
echo "Game directory: $GAME_DIR"

if [ ! -d "$GAME_DIR" ]; then
    echo "ERROR: Target directory does not exist: $GAME_DIR"
    echo "Usage: ./install.sh [path_to_game_directory]"
    echo "Or set GAME_DIR environment variable: GAME_DIR=\"/path/to/game\" ./install.sh"
    exit 1
fi

# 1. Build mod
echo "[1/4] Building mod DLL..."
cd "$SCRIPT_DIR/src/TacticalAssaultFPV"
dotnet build -c Release

# 2. Install MelonLoader core if present
if [ -d "$SCRIPT_DIR/tools/melonloader" ]; then
    echo "[2/4] Installing MelonLoader core files..."
    [ -f "$SCRIPT_DIR/tools/melonloader/version.dll" ] && cp -u "$SCRIPT_DIR/tools/melonloader/version.dll" "$GAME_DIR/"
    mkdir -p "$GAME_DIR/MelonLoader"
    cp -ru "$SCRIPT_DIR/tools/melonloader/MelonLoader/"* "$GAME_DIR/MelonLoader/"
else
    echo "[2/4] Skipping MelonLoader core installation (tools/melonloader not present; assume pre-installed in game)."
fi

# 3. Pre-seed generated Il2CppAssemblies if present
if [ -d "$SCRIPT_DIR/interop" ]; then
    echo "[3/4] Pre-seeding Il2CppAssemblies..."
    mkdir -p "$GAME_DIR/MelonLoader/Il2CppAssemblies"
    cp -ru "$SCRIPT_DIR/interop/"*.dll "$GAME_DIR/MelonLoader/Il2CppAssemblies/" 2>/dev/null || true
else
    echo "[3/4] Skipping Il2CppAssemblies pre-seeding (MelonLoader will generate them automatically on first launch)."
fi

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
