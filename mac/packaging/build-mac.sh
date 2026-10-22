#!/usr/bin/env bash
# Build, assemble, ad-hoc sign and package Softstage for macOS.
#
# Usage: build-mac.sh <version> <arch>
#   version  numeric X.Y.Z, for example 0.1.0
#   arch     arm64 | x64
#
# Must run on macOS. Writes:
#   dist/Softstage-<version>-macos-<arch>.dmg
#   dist/Softstage-<version>-macos-<arch>.dmg.sha256
#
# The app is ad-hoc signed only. It is not Developer ID signed and not notarized.

set -euo pipefail

usage() {
  echo "Usage: $0 <version> <arch>" >&2
  echo "  version: numeric X.Y.Z, for example 0.1.0" >&2
  echo "  arch:    arm64 or x64" >&2
  exit 2
}

if [ "$(uname -s)" != "Darwin" ]; then
  echo "error: build-mac.sh must run on macOS (uname reports $(uname -s))." >&2
  exit 1
fi

if [ "$#" -ne 2 ]; then
  usage
fi

VERSION="$1"
ARCH="$2"

case "$ARCH" in
  arm64) RID="osx-arm64" ;;
  x64) RID="osx-x64" ;;
  *)
    echo "error: unknown arch '$ARCH' (expected arm64 or x64)." >&2
    usage
    ;;
esac

if ! [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "error: version must be numeric X.Y.Z, got '$VERSION'." >&2
  exit 2
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$REPO_ROOT"

PROJECT="mac/Softstage.Mac/Softstage.Mac.csproj"
TEMPLATE_PLIST="mac/packaging/Info.plist.template"
QUICK_ACTION_SRC="mac/packaging/quick-action/Create composition with Softstage.workflow"
ICON_SRC="assets/app-icon.png"

PUBLISH_DIR="build/publish-$ARCH"
APP_DIR="build/Softstage.app"
STAGE_DIR="build/dmg-$ARCH"
DMG_NAME="Softstage-$VERSION-macos-$ARCH.dmg"
DMG_PATH="dist/$DMG_NAME"

for required in "$PROJECT" "$TEMPLATE_PLIST" "$QUICK_ACTION_SRC" "$ICON_SRC"; do
  if [ ! -e "$required" ]; then
    echo "error: missing $required" >&2
    exit 1
  fi
done

if ! command -v dotnet >/dev/null 2>&1; then
  echo "error: dotnet is not on PATH." >&2
  exit 1
fi

echo "==> Cleaning previous output for $ARCH"
rm -rf "$PUBLISH_DIR" "$STAGE_DIR"
rm -rf "$APP_DIR"
rm -f "$DMG_PATH" "$DMG_PATH.sha256"
mkdir -p dist

echo "==> Publishing $PROJECT for $RID (version $VERSION)"
dotnet publish "$PROJECT" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -p:PublishSingleFile=false \
  -p:Version="$VERSION" \
  -o "$PUBLISH_DIR"

if [ ! -f "$PUBLISH_DIR/Softstage" ]; then
  echo "error: expected executable $PUBLISH_DIR/Softstage was not produced." >&2
  exit 1
fi

echo "==> Assembling $APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources/QuickAction"

cp -R "$PUBLISH_DIR/." "$APP_DIR/Contents/MacOS/"
chmod +x "$APP_DIR/Contents/MacOS/Softstage"

sed "s/__VERSION__/$VERSION/g" "$TEMPLATE_PLIST" > "$APP_DIR/Contents/Info.plist"

bash "$SCRIPT_DIR/make-icns.sh" "$ICON_SRC" "$APP_DIR/Contents/Resources/AppIcon.icns"

cp -R "$QUICK_ACTION_SRC" "$APP_DIR/Contents/Resources/QuickAction/"

echo "==> Ad-hoc signing $APP_DIR"
codesign --force --deep --sign - "$APP_DIR"
codesign --verify --deep --strict "$APP_DIR"

echo "==> Creating $DMG_PATH"
mkdir -p "$STAGE_DIR"
ditto "$APP_DIR" "$STAGE_DIR/Softstage.app"
ln -s /Applications "$STAGE_DIR/Applications"

hdiutil create \
  -volname Softstage \
  -srcfolder "$STAGE_DIR" \
  -ov \
  -format UDZO \
  "$DMG_PATH"

(cd dist && shasum -a 256 "$DMG_NAME" > "$DMG_NAME.sha256")

rm -rf "$STAGE_DIR"

echo "==> Done"
echo "App:    $REPO_ROOT/$APP_DIR"
echo "DMG:    $REPO_ROOT/$DMG_PATH"
echo "SHA256: $REPO_ROOT/$DMG_PATH.sha256"
