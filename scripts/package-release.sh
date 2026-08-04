#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MOD_NAME="YellowFlareCurse"
MOD_VERSION="1.4.5"
DIST="$ROOT/Dist"
STAGE="$DIST/stage"
ZIP_NAME="${MOD_NAME}-${MOD_VERSION}.zip"

CLIENT_DLL="$ROOT/Build/BepInEx/plugins/YellowFlareCurse.Client.dll"
SERVER_DIR="$ROOT/Build/SPT/user/mods/YellowFlareCurse"

if [[ ! -f "$CLIENT_DLL" ]]; then
  echo "Missing client DLL. Build Client first." >&2
  exit 1
fi
if [[ ! -f "$SERVER_DIR/YellowFlareCurse.dll" ]]; then
  echo "Missing server DLL. Build Server first." >&2
  exit 1
fi

rm -rf "$DIST"
mkdir -p "$STAGE/BepInEx/plugins"
mkdir -p "$STAGE/SPT/user/mods/YellowFlareCurse"

cp "$CLIENT_DLL" "$STAGE/BepInEx/plugins/"
cp -R "$SERVER_DIR/." "$STAGE/SPT/user/mods/YellowFlareCurse/"

(
  cd "$STAGE"
  zip -r "$DIST/$ZIP_NAME" BepInEx SPT >/dev/null
)

echo "Created $DIST/$ZIP_NAME"
unzip -l "$DIST/$ZIP_NAME"
