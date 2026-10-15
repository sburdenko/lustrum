#!/usr/bin/env bash
# Usage: package-linux.sh <app-publish-dir> <output-appimage>
set -euo pipefail

app_dir="$1"
output="$2"
root="$(cd "$(dirname "$0")/.." && pwd)"
stage="$(mktemp -d)"
appdir="$stage/Lustrum.AppDir"
tool_url="https://github.com/AppImage/AppImageKit/releases/download/13/appimagetool-x86_64.AppImage"

mkdir -p "$appdir/usr/bin"
cp -R "$app_dir/." "$appdir/usr/bin/"
cp "$root/build/linux/lustrum.desktop" "$appdir/lustrum.desktop"
cp "$root/src/Lustrum.App/Assets/lustrum.png" "$appdir/lustrum.png"
cat > "$appdir/AppRun" <<'RUN'
#!/bin/sh
exec "$(dirname "$(readlink -f "$0")")/usr/bin/Lustrum" "$@"
RUN
chmod +x "$appdir/AppRun" "$appdir/usr/bin/Lustrum"

curl -fsSL -o "$stage/appimagetool" "$tool_url"
chmod +x "$stage/appimagetool"
mkdir -p "$(dirname "$output")"
ARCH=x86_64 "$stage/appimagetool" --appimage-extract-and-run "$appdir" "$output"
rm -rf "$stage"
