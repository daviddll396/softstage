#!/usr/bin/env bash
# Build an .icns file from a square PNG using sips and iconutil.
#
# Usage: make-icns.sh <source.png> <output.icns>
# Must run on macOS (uses sips and iconutil).

set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "Usage: $0 <source.png> <output.icns>" >&2
  exit 2
fi

if [ "$(uname -s)" != "Darwin" ]; then
  echo "error: make-icns.sh must run on macOS (needs sips and iconutil)." >&2
  exit 1
fi

SRC="$1"
OUT="$2"

if [ ! -f "$SRC" ]; then
  echo "error: source image not found: $SRC" >&2
  exit 1
fi

TMP_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/softstage-icns.XXXXXX")"
trap 'rm -rf "$TMP_ROOT"' EXIT

ICONSET="$TMP_ROOT/AppIcon.iconset"
mkdir -p "$ICONSET"

for size in 16 32 128 256 512; do
  double=$((size * 2))
  sips -z "$size" "$size" "$SRC" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
  sips -z "$double" "$double" "$SRC" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
done

mkdir -p "$(dirname "$OUT")"
rm -f "$OUT"
iconutil -c icns "$ICONSET" -o "$OUT"

echo "Wrote $OUT"
