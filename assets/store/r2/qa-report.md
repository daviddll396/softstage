# Softstage Store trailer R2 QA

## Output checks

ffprobe confirmed each MP4 file.

| File | Size | Picture | Length | Audio |
| --- | ---: | --- | ---: | --- |
| SoftstageStoreTrailerR2-preview.mp4 | 32,620,783 bytes | 1920 x 1080, 30 fps, 933 frames, H.264, about 8 Mbps | 31.1 s | AAC stereo, 48 kHz |
| SoftstageStoreTrailerR2-master.mp4 | 194,647,187 bytes | 1920 x 1080, 30 fps, 933 frames, H.264, about 50 Mbps | 31.1 s | AAC stereo, 48 kHz |
| SoftstageStoreTrailerR2-silent.mp4 | 193,140,709 bytes | 1920 x 1080, 30 fps, 933 frames, H.264, about 50 Mbps | 31.1 s | None |
| SoftstageStoreTrailerR2-review.mp4 | 7,034,695 bytes | 960 x 540, 30 fps, 933 frames, H.264, about 1.6 Mbps | 31.1 s | AAC stereo, 48 kHz |

The full stream data and SHA-256 checksums are in manifest.json. The preview and review files include quiet transition effects. The master is the high bitrate Store export. The silent file has the same picture as the master.

## Picture review

- The edit uses real screen recordings for the selection, context menu, Softstage command, PNG output, and double-click.
- Multiple recordings are edited together. The video does not claim to be one uncut take.
- The command close-up shows the real pointer hover and click. It enlarges the recorded menu row for legibility.
- The Explorer result view shows the three source images and one generated PNG tile. The crop excludes the preview pane.
- The zoom uses the real PNG thumbnail. The full tile stays in frame during the centered zoom.
- The last Explorer frame is frame 752. It shows one output tile and the pointer. Frame 753 cuts to the full Photos recording. There is no overlapping second copy or black launch gap.
- The Photos shot shows one full-window composition and no filmstrip.
- The final frame count is 933. The edit runs for 31.1 seconds at 30 fps.

## Code check

Ran npx tsc --noEmit from video/ after the final composition edit; it passed. No application tests were run.

## Release limits

Do not stage or distribute assets/store/r2/raw/. The raw folder includes private source recordings and one unused black Photos capture. Do not stage the unused video/public/media/store-r2/explorer-cropped.mp4 proxy. Local renders and QA images are in the ignored artifacts/store-media-r2/ directory.
