#!/usr/bin/env bash
# Signs, notarizes and zips MegaDownloader.app for distribution.
#
#   ./packaging/macos/sign.sh <app> "<Developer ID Application: Name (TEAMID)>" [notary-profile]
#
# The notary profile is a keychain item created once with:
#   xcrun notarytool store-credentials megadownloader-notary \
#     --apple-id <id> --team-id <TEAMID> --password <app-specific-password>
#
# Without a profile the script stops after signing, so a local build can still be
# launched with Gatekeeper's "Open Anyway" path.
set -euo pipefail

app=${1:?usage: sign.sh <app> "<identity>" [notary-profile]}
identity=${2:?usage: sign.sh <app> "<identity>" [notary-profile]}
profile=${3:-}

if [ ! -d "$app" ]; then
  echo "No bundle at $app" >&2
  exit 1
fi

# Nested code (the MEGA account dylib) must be signed before the bundle that contains it.
find "$app/Contents/MacOS" -type f \( -name "*.dylib" -o -name "*.so" \) -print0 |
  while IFS= read -r -d '' lib; do
    codesign --force --timestamp --options runtime --sign "$identity" "$lib"
  done

codesign --force --timestamp --options runtime --sign "$identity" "$app"
codesign --verify --deep --strict --verbose=2 "$app"

if [ -z "$profile" ]; then
  echo "Signed but not notarized (no keychain profile given)."
  exit 0
fi

zip_path="${app%.app}.zip"
rm -f "$zip_path"
/usr/bin/ditto -c -k --keepParent "$app" "$zip_path"
xcrun notarytool submit "$zip_path" --keychain-profile "$profile" --wait
xcrun stapler staple "$app"
rm -f "$zip_path"
/usr/bin/ditto -c -k --keepParent "$app" "$zip_path"

# The stapled ticket is what lets a first launch work offline.
spctl --assess --type execute --verbose=4 "$app"
echo "Notarized and zipped: $zip_path"
