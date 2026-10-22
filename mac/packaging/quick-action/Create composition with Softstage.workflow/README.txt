Create composition with Softstage - Finder Quick Action bundle

This folder is an Automator Quick Action (.workflow) bundle. The Softstage
app copies it into the user's Library when the Finder action is enabled.

Placeholder: Contents/document.wflow contains the string
"__SOFTSTAGE_EXECUTABLE__" inside COMMAND_STRING. The Softstage app replaces
that placeholder with the path to the Softstage executable at install time.
The quotes around the placeholder are part of the command and must be kept.
Do not use this bundle as-is: the placeholder must be substituted first.

Status: this bundle structure has NOT been tested on macOS. It was written by
hand from Automator's plist format. It has not been opened in Automator, and
the Finder menu entry has not been checked.
