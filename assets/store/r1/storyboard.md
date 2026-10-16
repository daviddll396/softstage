# Softstage Store trailer storyboard

## Format

- Runtime: 24 seconds.
- Frame rate: 30 fps.
- Frame size: 1920 x 1080.
- Total: 720 frames.
- Sound: quiet transition effects only. No voice or music.

## Timeline

| Time | Frames | Picture and action | Text and sound |
| --- | ---: | --- | --- |
| 0-2 s | 0-59 | The Softstage icon and wordmark enter on charcoal. A lilac sheen crosses the S mark. | No product claim. One quiet transition cue. |
| 2-7 s | 60-209 | Hold a real Windows 11 Explorer capture with the three source images selected. The source still is 1215 x 642 and is shown at its native size. | **STEP 1** / Select three images. One quiet cue at the scene start. |
| 7-12 s | 210-359 | Hold a real Windows 11 Explorer capture with the pointer over the highlighted Softstage command. The source still is 1215 x 642 and is shown at its native size. | **STEP 2** / Create with Softstage. One quiet cue at the scene start. |
| 12-17 s | 360-509 | Hold a real Explorer capture with the three original images and the new PNG visible and unselected. The source still is 1215 x 642 and is shown at its native size. | **STEP 3** / Your PNG is ready. One quiet cue at the scene start. |
| 17-21 s | 510-629 | Show the matching 3840 x 2160 composition created by Softstage. | “Your PNG is ready.” The image pushes in for 6 frames, recovers to 102% over 6 frames, then holds. |
| 21-24 s | 630-719 | Return to the Softstage icon and wordmark. | “Select. Right-click. Compose.” One quiet closing cue. No Store availability claim. |

## Capture record

The three steps use authentic Explorer states, but the trailer is not one continuous recording. The selection, command, and result stills are real captures from 14 October 2026. The selected-image still shows three selected files. The command still shows the pointer over the real Softstage command. The result still shows the same three source files and the new PNG, all unselected.

An 8-second context-menu recording was made on 16 October 2026. It is not used in the trailer because its pointer stays near “Open” while the Softstage command row appears bordered. It does not show a clear pointer hover or click. The selected still for step 2 is clearer and shows a real pointer over the command.

The composition in the reveal is a real Softstage output from 12 October 2026. Its image order matches the 14 October result still. A new output was also created on 16 October, but its panel order did not match the result still, so it is not used in this edit.

The project uses no invented product controls, file states, notifications, or cursor actions. It uses stills for all three steps and the matching real output image for the reveal. The trailer adds only restrained motion to the real captures.

## Store images

The four Store screenshots are 1920 x 1080 PNG files:

1. `exports/store-screenshot-01-select.png` - three images selected.
2. `exports/store-screenshot-02-command.png` - the real Explorer command highlighted.
3. `exports/store-screenshot-03-created.png` - the new PNG and three source images, unselected.
4. `exports/store-screenshot-04-composition.png` - the full composition output.

The first and third screenshots use their authentic 1215 x 642 capture at native size. The captions sit outside the captured UI.

## Motion and sound

The opening and closing are original brand motion. The output reveal is a custom camera move informed by the `crash-zoom-punch` card: a 6-frame ease-in push from 90% to 108%, a 6-frame recovery to 102%, and a long still hold. It is not a continuous copy of the card demo. The full composition remains inside the frame after the recovery.

All scene timing is in `video/src/SoftstageStoreTrailer.tsx` in `storeTimeline`. The four low-level effects use scene start frames from that timing table. The silent master removes the audio track from the same picture render.
