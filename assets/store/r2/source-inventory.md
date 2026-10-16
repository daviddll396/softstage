# Softstage Store trailer R2 source inventory

## Recordings and assets

| Source | Canonical path | Use | Record |
| --- | --- | --- | --- |
| Main Explorer take | `assets/store/r2/raw/workflow-take1.mp4` | Real selection, context menus, PNG output, and double-click. | Real Windows screen recording. H.264, 1778 x 1260, 120 seconds. Recorded 16 October 2026. The edit crops out the left sidebar, folder path, and preview pane. |
| Command close-up | `assets/store/r2/raw/command-click-final.mp4` | Real pointer hover and click on the Softstage command. | Real Windows screen recording. H.264, 1778 x 1260, 61.4 seconds. The edit uses 58.5-61.4 seconds and enlarges the real menu row 2.2x. The crop excludes two older output thumbnails. |
| Unused command capture | assets/store/r2/raw/command-hover-click.mp4 | Not used. | Real Windows screen recording. H.264, 1778 x 1260, 35 seconds. It is an earlier command capture and is not part of the final edit. |
| Full Photos view | `assets/store/r2/raw/opened-composition-desktop.mp4` | Show the opened output. | Real Photos window recording. H.264, 2862 x 1548, about 5.97 seconds. The shot shows the generated composition in one full-window image. |
| Unused Photos capture | `assets/store/r2/raw/opened-composition.mp4` | Not used. | This capture is black. It stays in the raw folder and is excluded from release files. |
| Explorer focused proxy | `video/public/media/store-r2/explorer-focused.mp4` | Selection and menu scenes. | Made from the main Explorer take. Cropped at x=250, y=155 to 1000 x 560, then scaled to 1920 x 1080 at 30 fps. It removes the left sidebar and folder path. The edit uses a small scale and vertical offset to keep the cards and pointer in frame. |
| Explorer result proxy | `video/public/media/store-r2/explorer-created.mp4` | PNG-created and open-PNG scenes. | Made from the main Explorer take. Cropped at x=250, y=155 to 1200 x 675, then scaled to 1920 x 1080 at 30 fps. It shows three source files and exactly one output PNG tile. The crop ends before the preview pane at x=1747. The open action stays at 1x framing so all four tiles remain visible. |
| Command proxy | `video/public/media/store-r2/command-closeup.mp4` | Menu hover and click. | Made from the command recording. Cropped at x=267, y=155 to 852 x 860, then placed in a 1920 x 1080 frame at 30 fps. It removes the two older output thumbnails. Remotion enlarges the real command row 2.2x. |
| Photos proxy | `video/public/media/store-r2/photos-window.mp4` | Open composition scene. | Made from the real Photos recording. Scaled to fit in 1920 x 1080 and padded with the Softstage charcoal color at 30 fps. It shows no filmstrip. The edit hard-cuts to this recording after the real double-click. |
| Softstage icon and fonts | `video/public/media/store-r1/` | Opening and closing. | Existing Softstage icon and Google Sans Flex assets. See the R1 source inventory for details. |
| Transition sound | `video/public/audio/swoosh-quick.mp3` | Quiet opening and Photos transition cues. | Mixkit sound effect 166. Use it only inside video output. Source: <https://assets.mixkit.co/active_storage/sfx/166/166-preview.mp3>. Do not redistribute it by itself. |

## Edit source

The composition and source timing are in `video/src/SoftstageStoreTrailerR2.tsx`. The trailer edits several real recordings together. It does not present them as one take. Captions and brand motion are added around the footage. The selection, menus, output, cursor movement, command click, and double-click use real screen recordings.

The edit shows exactly one generated composition at a time. It first appears as one PNG tile in Explorer. After the real double-click, the video cuts to one full-window Photos view. It does not show the Explorer tile and Photos view at once. It does not show a second Explorer preview or Photos filmstrip.

## Files kept out of the release

The raw recordings in `assets/store/r2/raw/` are working sources. Do not stage them or include them in the Store package. This includes the unused black `opened-composition.mp4` capture. The old `video/public/media/store-r2/explorer-cropped.mp4` proxy is unused. The files in `artifacts/store-media-r2/` are local renders and QA images, not source media.
