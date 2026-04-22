#!/usr/bin/env bash
# Sync names and image assets from a Slay the Spire 2 .pck into this repo:
#   1. Runs the Go extractor into a temp dir.
#   2. Merges new card/relic/potion/character/monster names into
#      web/static/names.json (existing entries are preserved, so records that
#      reference deprecated IDs keep working).
#   3. Transcodes potion and relic PNGs to 128x128 WebP and copies them into
#      web/static/assets/{potions,relics}/.
#
# Usage: scripts/sync-from-extract.sh [path-to-pck]
# Default .pck path is the macOS Steam install.

set -euo pipefail

pck="${1:-$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/Resources/Slay the Spire 2.pck}"
if [[ ! -f "$pck" ]]; then
  echo "error: pck not found: $pck" >&2
  echo "usage: $0 [path-to-pck]" >&2
  exit 1
fi

for bin in cwebp go node; do
  if ! command -v "$bin" >/dev/null 2>&1; then
    echo "error: $bin not on PATH" >&2
    exit 1
  fi
done

repo_root=$(cd "$(dirname "$0")/.." && pwd)
extract_dir=$(mktemp -d -t sts2-extract.XXXXXX)
trap 'rm -rf "$extract_dir"' EXIT

echo "== extracting to $extract_dir =="
(cd "$repo_root/extract" && go run . -out "$extract_dir" "$pck")

echo "== merging names into web/static/names.json =="
(cd "$repo_root/web" && node --experimental-strip-types scripts/build-names.ts "$extract_dir/localization/eng")

copy_webps() {
  local kind="$1" # potions | relics
  local src_dir="$extract_dir/$kind"
  local dst_dir="$repo_root/web/static/assets/$kind"
  mkdir -p "$dst_dir"
  if [[ ! -d "$src_dir" ]]; then
    echo "  (no $kind extracted)"
    return
  fi
  local n=0
  shopt -s nullglob
  for png in "$src_dir"/*.png; do
    cwebp -quiet -q 80 -resize 128 128 -m 6 "$png" -o "${png%.png}.webp"
    cp "${png%.png}.webp" "$dst_dir/"
    n=$((n + 1))
  done
  shopt -u nullglob
  echo "  $kind: $n webp(s) in $dst_dir"
}

echo "== transcoding PNG → WebP =="
copy_webps potions
copy_webps relics

echo "done."
