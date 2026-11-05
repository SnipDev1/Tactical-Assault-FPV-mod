#!/usr/bin/env bash
GAME_DIR="/mnt/olddisk/SteamLibrary/steamapps/common/Tactical Assault VR"

echo "=== Uninstalling TacticalAssaultFPV Mod from Tactical Assault VR ==="
rm -f "$GAME_DIR/version.dll"
rm -rf "$GAME_DIR/MelonLoader"
rm -rf "$GAME_DIR/Mods"
rm -rf "$GAME_DIR/UserData"
echo "Done! The game directory has been reverted to vanilla."
