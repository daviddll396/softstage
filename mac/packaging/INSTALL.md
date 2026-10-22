# Install Softstage for Mac

Softstage for Mac needs macOS 13.0 or later.

1. Download from the Releases page:
   - `Softstage-<version>-macos-arm64.dmg` for Apple silicon Macs
   - `Softstage-<version>-macos-x64.dmg` for Intel Macs
   
   To check a download, put the DMG and its `.dmg.sha256` file in the same folder and run `shasum -a 256 -c <name>.dmg.sha256`.
2. Open the DMG and drag Softstage into Applications.
3. Open Softstage once. The app is not notarized by Apple, so macOS blocks the first launch. Use one of these:
   - In Applications, right-click (or Control-click) Softstage, choose Open, then click Open in the dialog.
   - Or run this in Terminal:
     ```
     xattr -dr com.apple.quarantine /Applications/Softstage.app
     ```
   
   Newer macOS versions may have moved the right-click route to System Settings, Privacy & Security, where you click Open Anyway. That path and the right-click route are not tested on this build. The Terminal command is the fallback.

## Finder right-click action

Softstage installs a Finder Quick Action named "Create composition with Softstage" from inside the app. Finder steps are unverified on this build.

If the action does not appear in Finder's right-click menu:

1. Restart Finder (hold Option, right-click the Finder icon in the Dock, then choose Relaunch).
2. Open System Settings, Keyboard, Keyboard Shortcuts, Services. Make sure "Create composition with Softstage" is ticked.
