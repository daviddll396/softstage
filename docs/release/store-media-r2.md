# Softstage Store media R2

## Deliverables

The final videos are in the local, Git-ignored folder artifacts/store-media-r2/:

- SoftstageStoreTrailerR2-preview.mp4 - 1920 x 1080 shareable preview with quiet sound effects. Runtime: 31.1 seconds.
- SoftstageStoreTrailerR2-master.mp4 - 1920 x 1080 Store master with quiet sound effects. Video bitrate: about 50 Mbps.
- SoftstageStoreTrailerR2-silent.mp4 - same picture as the master, with no audio stream.
- SoftstageStoreTrailerR2-review.mp4 - 960 x 540 review copy. Do not use it as the Store master.

The trailer runs at 30 fps and has 933 frames. The manifest lists file sizes, stream details, and SHA-256 checksums.

## Real recorded workflow

The edit shows a real Windows workflow: select three images, open the context menu, hover over and click the Softstage command, see one generated PNG tile, zoom into that tile, and double-click it. A hard cut after the recorded double-click moves to a separate real Photos recording.

The trailer edits multiple real screen recordings together. It is not one uncut take. It shows exactly one generated composition at a time. The Explorer crop removes the preview pane and older output thumbnails. The Photos recording shows one full-window image and no filmstrip. No cursor, menu, output, or Windows control is recreated.

The edit adds Softstage branding, captions, camera motion, and quiet transition effects. It does not include voice or music. The source sound effect is not included as a standalone release file.

## Source and release files

The source inventory, sequence brief, storyboard, manifest, and QA report are in assets/store/r2/. The cropped video proxies used by Remotion are in video/public/media/store-r2/.

The raw recordings in assets/store/r2/raw/ are private working files. Do not stage or distribute them. This includes the unused black opened-composition.mp4 recording. The explorer-cropped.mp4 proxy is also unused. Render files and QA images stay in the ignored artifacts/store-media-r2/ folder.

## Checks and limits

From video/, npx tsc --noEmit passed. ffprobe confirmed the stream details for all four final MP4 files. The visual checks and exact output metadata are in assets/store/r2/qa-report.md and assets/store/r2/manifest.json.

These files do not prove Store approval, signing, or purchase availability.
