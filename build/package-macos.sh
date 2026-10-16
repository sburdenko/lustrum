#!/usr/bin/env bash
# Usage: package-macos.sh <version> <app-publish-dir> <cli-publish-dir> <output-zip>
set -euo pipefail

version="$1"
app_dir="$2"
cli_dir="$3"
output="$4"
root="$(cd "$(dirname "$0")/.." && pwd)"
stage="$(mktemp -d)"
bundle="$stage/Lustrum.app"

mkdir -p "$bundle/Contents/MacOS" "$bundle/Contents/Resources" "$stage/cli"
cp -R "$app_dir/." "$bundle/Contents/MacOS/"
cp "$root/src/Lustrum.App/Assets/lustrum.icns" "$bundle/Contents/Resources/lustrum.icns"
sed "s/__VERSION__/$version/g" "$root/build/macos/Info.plist" > "$bundle/Contents/Info.plist"
cp -R "$cli_dir/." "$stage/cli/"

codesign --force --deep --sign - "$bundle"
codesign --force --sign - "$stage/cli/lustrum"

mkdir -p "$(dirname "$output")"
ditto -c -k --sequesterRsrc "$stage" "$output"
rm -rf "$stage"
