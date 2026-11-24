#!/usr/bin/env bash
# Builds MegaDownloader.app from a published self-contained osx-arm64 build.
#
#   ./packaging/macos/make-app.sh <publish-dir> <output-dir> [version]
#
# The publish directory must be the output of:
#   dotnet publish MegaDownloader.UI/MegaDownloader.UI.csproj -c Release \
#     -r osx-arm64 --self-contained true -p:PublishSingleFile=true
#
# Signing and notarization are separate steps (see sign.sh) because they need a
# Developer ID certificate and an Apple account, neither of which belongs in CI.
set -euo pipefail

publish_dir=${1:?usage: make-app.sh <publish-dir> <output-dir> [version]}
output_dir=${2:?usage: make-app.sh <publish-dir> <output-dir> [version]}
version=${3:-2.9.0}

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
repo_root=$(cd "$script_dir/../.." && pwd)
app="$output_dir/MegaDownloader.app"
contents="$app/Contents"

rm -rf "$app"
mkdir -p "$contents/MacOS" "$contents/Resources"

# The single-file publish puts the executable and its native companions in one folder.
cp -R "$publish_dir"/. "$contents/MacOS/"
chmod +x "$contents/MacOS/MegaDownloader"

sed "s/__VERSION__/$version/g" "$repo_root/packaging/Info.plist" > "$contents/Info.plist"

# The icon is a 256x256 PNG extracted from the Windows .ico; macOS accepts a PNG in
# CFBundleIconFile only after it is wrapped in an .icns, so build one when the tools
# are available and keep the PNG as a fallback.
icon_src="$repo_root/packaging/icons/megadownloader.png"
if [ -f "$icon_src" ]; then
  cp "$icon_src" "$contents/Resources/megadownloader.png"
  if command -v iconutil >/dev/null 2>&1 && command -v sips >/dev/null 2>&1; then
    iconset=$(mktemp -d)/MegaDownloader.iconset
    mkdir -p "$iconset"
    for size in 16 32 64 128 256 512; do
      sips -z $size $size "$icon_src" --out "$iconset/icon_${size}x${size}.png" >/dev/null
      double=$((size * 2))
      sips -z $double $double "$icon_src" --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
    done
    iconutil -c icns "$iconset" -o "$contents/Resources/MegaDownloader.icns"
  fi
fi

# A plist that does not parse makes the bundle unlaunchable, and the failure only
# shows up when a user double-clicks it.
if command -v plutil >/dev/null 2>&1; then
  plutil -lint "$contents/Info.plist"
fi

echo "Built $app"
echo "Next: ./packaging/macos/sign.sh $app <Developer ID Application: ...>"
