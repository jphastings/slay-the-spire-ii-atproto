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

# Sprite-sheet manifests and other small JSONs go into src/lib/data so
# vite bundles them into the JS chunks at build time — saves a round
# trip per file at runtime. The webp/png binaries stay in static/.
data_dir="$repo_root/web/src/lib/data"
mkdir -p "$data_dir"

copy_sprite() {
  local kind="$1" # potions | relics
  local png="$extract_dir/${kind}_sprite.png"
  local json="$extract_dir/${kind}_sprite.json"
  local dst_dir="$repo_root/web/static/assets"
  if [[ ! -f "$png" ]] || [[ ! -f "$json" ]]; then
    echo "  (no $kind sprite extracted)"
    return
  fi
  mkdir -p "$dst_dir"
  cwebp -quiet -q 80 -m 6 "$png" -o "$dst_dir/${kind}_sprite.webp"
  cp "$json" "$data_dir/${kind}.json"
  # Drop any previously-committed per-icon webps (superseded by the sprite).
  rm -rf "$dst_dir/$kind"
  # Old (pre-bundling) layout shipped the manifest in static/assets;
  # remove it so we don't ship the same data twice.
  rm -f "$dst_dir/${kind}_sprite.json"
  echo "  $kind: $dst_dir/${kind}_sprite.webp + $data_dir/${kind}.json"
}

echo "== building sprite sheets =="
copy_sprite potions
copy_sprite relics
copy_sprite orb
copy_sprite enchant
copy_sprite characters

copy_portraits() {
  local src_root="$extract_dir/card_portraits"
  local dst_root="$repo_root/web/static/assets/card_portraits"
  local data_root="$data_dir/portraits"
  if [[ ! -d "$src_root" ]]; then
    echo "  (no card portraits extracted)"
    return
  fi
  # Replace per-character sheets wholesale; drop any per-card webps from the
  # previous layout (one webp per portrait would blow past wisp's chunker).
  rm -rf "$dst_root" "$data_root"
  mkdir -p "$dst_root" "$data_root"
  shopt -s nullglob
  local total=0
  for png in "$src_root"/*_sprite.png; do
    local char
    char=$(basename "${png%_sprite.png}")
    cwebp -quiet -q 75 -m 6 "$png" -o "$dst_root/$char.webp"
    cp "$src_root/${char}_sprite.json" "$data_root/$char.json"
    total=$((total + 1))
  done
  shopt -u nullglob
  echo "  card_portraits: $total sheets → $dst_root + $data_root"
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

  # Fonts are raw TTFs — no transcoding.
  cp -R "$src/fonts" "$dst/"

  # Bundled JSONs: cards.json (metadata) + tints.json (HSV per
  # frame-color and rarity, used to drive CSS hue-rotate filters).
  [[ -f "$src/cards.json" ]] && cp "$src/cards.json" "$data_dir/cards.json"
  [[ -f "$src/tints.json" ]] && cp "$src/tints.json" "$data_dir/tints.json"

  # Card parts: frame/portrait_border bases (3 each, hue-tinted in CSS),
  # banner.png + plaque.png (single shapes, also hue-tinted in CSS).
  # Orbs and the enchant tab + icons ship as sprite sheets (copy_sprite
  # orb / enchant) — skip their raw PNGs here.
  local parts_count=0
  if [[ -d "$src/parts" ]]; then
    while IFS= read -r png; do
      local rel="${png#$src/parts/}"
      local out="$dst/parts/${rel%.png}.webp"
      mkdir -p "$(dirname "$out")"
      cwebp -quiet -q 80 -m 6 "$png" -o "$out"
      parts_count=$((parts_count + 1))
    done < <(find "$src/parts" -name "*.png" -type f -not -path "*/orb/*")
  fi

  echo "  cards: $parts_count parts → webp in $dst"
}
copy_cards

# Old static-fetched copies of the bundled JSONs would otherwise linger.
rm -f "$repo_root/web/static/names.json" "$repo_root/web/static/cards/cards.json"

echo "done."
