#!/usr/bin/env bash
# Bundle a published macOS dotnet output into a .app bundle.
# Usage: scripts/bundle-macos-app.sh <publish-dir> <version> <output-dir>
set -euo pipefail

# Usage: scripts/bundle-macos-app.sh <publish-dir> <version> <output-dir>
publish_dir="$1"
version="${2:-0.0.0}"
outdir="${3:-.}"

app_name="atproto-tracker-installer"
app="$outdir/$app_name.app"

rm -rf "$app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"

# Copy the main binary + all native libraries (.dylib) from the publish dir.
cp "$publish_dir/$app_name" "$app/Contents/MacOS/$app_name"
chmod +x "$app/Contents/MacOS/$app_name"
for dylib in "$publish_dir"/*.dylib; do
  [ -f "$dylib" ] && cp "$dylib" "$app/Contents/MacOS/"
done

cat > "$app/Contents/Info.plist" << PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>atproto-tracker installer</string>
    <key>CFBundleDisplayName</key>
    <string>atproto-tracker installer</string>
    <key>CFBundleIdentifier</key>
    <string>me.byjp.atproto-tracker.installer</string>
    <key>CFBundleVersion</key>
    <string>$version</string>
    <key>CFBundleShortVersionString</key>
    <string>$version</string>
    <key>CFBundleExecutable</key>
    <string>$app_name</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
</dict>
</plist>
PLIST

echo "Created $app"
