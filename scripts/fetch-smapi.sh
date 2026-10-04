#!/usr/bin/env bash
# Downloads the pinned SMAPI release and extracts StardewModdingAPI.dll, SMAPI.Toolkit.CoreInterfaces.dll (the ISemanticVersion/IManifest types) and SMAPI's bundled 0Harmony.dll into lib/.
set -euo pipefail

VERSION=4.5.2
INSTALLER_SHA256=dd01ddca7b566bfe0d3b3d2d03833496abc56c53da976241f2ab443f5484acc4

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
out="$root/lib"
[ -f "$out/StardewModdingAPI.dll" ] && [ -f "$out/SMAPI.Toolkit.CoreInterfaces.dll" ] && [ -f "$out/0Harmony.dll" ] && exit 0

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
zip="$work/installer.zip"
curl -fsSL -o "$zip" "https://github.com/Pathoschild/SMAPI/releases/download/$VERSION/SMAPI-$VERSION-installer.zip"
echo "$INSTALLER_SHA256  $zip" | sha256sum -c -

# The installer nests the game-side files in a zip named install.dat.
unzip -q -j "$zip" "SMAPI $VERSION installer/internal/linux/install.dat" -d "$work"
mkdir -p "$out"
unzip -q -j -o "$work/install.dat" StardewModdingAPI.dll smapi-internal/SMAPI.Toolkit.CoreInterfaces.dll smapi-internal/0Harmony.dll -d "$out"
