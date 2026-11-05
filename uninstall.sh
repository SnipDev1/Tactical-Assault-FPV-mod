#!/usr/bin/env bash
DEFAULT_GAME_DIR="/mnt/olddisk/SteamLibrary/steamapps/common/Tactical Assault VR"
GAME_DIR="${1:-${GAME_DIR:-$DEFAULT_GAME_DIR}}"

echo "=== Uninstalling TacticalAssaultFPV Mod from Tactical Assault VR ==="
echo "Target directory: $GAME_DIR"

if [ ! -d "$GAME_DIR" ]; then
    echo "ERROR: Game directory does not exist: $GAME_DIR"
    echo "Usage: ./uninstall.sh [path_to_game_directory]"
    exit 1
fi

rm -f "$GAME_DIR/version.dll"
rm -f "$GAME_DIR/Mods/TacticalAssaultFPV.dll"
rm -rf "$GAME_DIR/MelonLoader"
rm -rf "$GAME_DIR/UserData"
echo "Done! The game directory has been reverted to vanilla."
