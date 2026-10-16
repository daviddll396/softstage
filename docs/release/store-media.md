# Softstage Store media, revision 1

## Deliverables

Four 1920 x 1080 Store screenshots, text-free hero art, a trailer thumbnail, English captions, and the source records are in `assets/store/r1/`. The master trailer and its silent version are in the local ignored folder `artifacts/store-media/`. The MP4 files are not committed because each is larger than 100 MB.

The trailer contains 720 frames at 30 fps. Its picture is 24 seconds. The master container is 24.067 seconds because the AAC audio ends on a codec frame boundary. The silent file has a 24-second container and no audio track.

## Capture record

The three Explorer scenes use real captures made on 14 October 2026. Step 1 shows three selected image files. Step 2 shows the pointer over the actual Softstage command. Step 3 shows the three source files and the generated PNG, all unselected. The matching Softstage composition was created on 12 October 2026.

These are separate still captures, not one continuous recording. The real 16 October menu clip did not show a clear pointer hover or click, so it is not used. The Store GUI screenshot was not captured. The trailer does not show a click or claim that the steps were filmed in one take.

## Video and sound

The master uses H.264/AVC High, 1920 x 1080, progressive yuv420p, 30 fps, 720 frames, a 15-frame closed GOP, two B-frames, CABAC, and BT.709 limited range. The measured video rate is 49.79 Mbps. The MP4 uses fast start and has no edit list.

The audio is AAC-LC, stereo, 48 kHz. Its measured rate is 380,989 bps; 384 kbps was the target. It contains low-volume Mixkit transition effects and no voice or music. The audio stream was probed, but the final mix was not listened to during this check. The source MP3 is ignored by Git because its license does not allow standalone redistribution. The sound effect source and license are in `assets/store/r1/manifest.json` and `video/README.md`.

## Source and checks

`video/src/SoftstageStoreTrailer.tsx` contains the Remotion source and `storeTimeline`. `video/src/workbench.ts` describes the complete trailer as one clip. It does not expose separate editable clips for each scene, caption, or sound cue. The one workbench launch attempt stopped at its first dependency-install step; it did not reach the browser preview.

`npm run build` passed. The final video probe and visual review passed. The opening output frame is visible; the output image matches the result capture. The manifest records output hashes, sizes, encode details, licenses, and capture limits.

These files are prepared media. They do not prove Store certification, signing, or purchase availability. Complete Partner Center checks and submit the app before making those claims.
