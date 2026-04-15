#!/usr/bin/env bash
# Generate reference-only assemblies for sts2.dll and 0Harmony.dll so that CI can
# build without shipping the game's proprietary DLLs. Commit mod/refs/ afterwards.
#
# Usage: scripts/generate-refs.sh [path-to-game-data-dir]
# If no path is passed, we read Sts2DataDir from mod/local.props.

set -euo pipefail

data_dir="${1:-}"
if [[ -z "$data_dir" ]]; then
  data_dir=$(awk -F '[<>]' '/<Sts2DataDir>/ { print $3 }' mod/local.props 2>/dev/null || true)
fi
if [[ -z "$data_dir" || ! -d "$data_dir" ]]; then
  echo "error: pass the game data dir as arg 1, or set Sts2DataDir in mod/local.props" >&2
  echo "example: scripts/generate-refs.sh '/path/to/SlayTheSpire2.app/Contents/Resources/data_sts2_macos_arm64'" >&2
  exit 1
fi

if ! command -v refasmer >/dev/null 2>&1; then
  echo "installing JetBrains.Refasmer.CliTool..."
  dotnet tool install -g JetBrains.Refasmer.CliTool
  export PATH="$PATH:$HOME/.dotnet/tools"
fi

mkdir -p mod/refs
refasmer -O mod/refs --omit-non-api-members=true \
  "$data_dir/sts2.dll" \
  "$data_dir/0Harmony.dll"

echo "done. Commit mod/refs/*.dll."
ls -la mod/refs/
