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

# Card metadata lives in class constructors in the game's sts2.dll and is
# only reachable via decompilation. Point ilspycmd at the real DLL next to
# the .pck (not the stub in mod/refs) and feed the output into the
# extractor.
ilspy="${DOTNET_ROLL_FORWARD:+env DOTNET_ROLL_FORWARD=LatestMajor }${HOME}/.dotnet/tools/ilspycmd"
decomp_flag=""
real_dll=$(dirname "$pck")/data_sts2_macos_arm64/sts2.dll
if [[ ! -f "$real_dll" ]]; then
  real_dll=$(dirname "$pck")/data_sts2_macos_x86_64/sts2.dll
fi
if [[ -f "$real_dll" ]] && [[ -x "${HOME}/.dotnet/tools/ilspycmd" ]]; then
  decomp_out="$extract_dir/decomp"
  mkdir -p "$decomp_out"
  DOTNET_ROLL_FORWARD=LatestMajor "${HOME}/.dotnet/tools/ilspycmd" "$real_dll" -o "$decomp_out" >/dev/null
  decomp_flag="-decompiled-cs $decomp_out/sts2.decompiled.cs"
fi

echo "== extracting to $extract_dir =="
# shellcheck disable=SC2086 # decomp_flag is intentionally word-split.
(cd "$repo_root/extract" && go run . -out "$extract_dir" $decomp_flag "$pck")

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

copy_portraits() {
  local src_root="$extract_dir/card_portraits"
  local dst_root="$repo_root/web/static/assets/card_portraits"
  if [[ ! -d "$src_root" ]]; then
    echo "  (no card portraits extracted)"
    return
  fi
  shopt -s nullglob
  local total=0
  for char_dir in "$src_root"/*/; do
    local char
    char=$(basename "$char_dir")
    local dst="$dst_root/$char"
    mkdir -p "$dst"
    for png in "$char_dir"*.png; do
      cwebp -quiet -q 75 -resize 356 0 -m 6 "$png" -o "${dst}/$(basename "${png%.png}").webp"
      total=$((total + 1))
    done
  done
  shopt -u nullglob
  echo "  card_portraits: $total webp(s) in $dst_root"
}
copy_portraits

copy_cards() {
  local src="$extract_dir/cards"
  local dst="$repo_root/web/static/cards"
  if [[ ! -d "$src" ]]; then
    echo "  (no card parts extracted)"
    return
  fi
  rm -rf "$dst"
  mkdir -p "$dst"
  # parts/ holds PNGs ready for the web — they're already alpha-compositable
  # and small (~5 MB total), so no WebP transcoding needed for v1.
  cp -R "$src/parts" "$src/fonts" "$dst/"
  [[ -f "$src/cards.json" ]] && cp "$src/cards.json" "$dst/"
  # Prune intermediate bases the extractor uses to bake tints.
  rm -rf "$dst/parts/frame_base" "$dst/parts/banner_base" \
         "$dst/parts/portrait_border_base" "$dst/parts/plaque_base.png"
  echo "  cards: synced parts/fonts/cards.json to $dst"
}
copy_cards

echo "done."
