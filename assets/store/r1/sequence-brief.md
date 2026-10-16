# Softstage Store trailer brief

## Goal

Show how a user selects three images, runs the Softstage Explorer command, and gets a composition PNG.

## Approved choices

| Item | Choice |
| --- | --- |
| Creation mode | Autonomous custom trailer, approved by the user |
| Runtime | 24 seconds at 30 fps, 1920 x 1080 |
| Workflow | Select three images; choose “Create composition with Softstage”; show the generated PNG beside the original files |
| Capture | Use genuine Explorer stills. State clearly that the captures use different dates. Do not simulate the missing continuous take or cursor action. |
| Motion | Use the Softstage brand lockup, gentle camera motion on the Explorer states, and one short punch into the real output. |
| Visual tokens | Charcoal `#121216`, lilac `#CBBFFF`, Google Sans Flex, and the current Softstage S icon. |
| Sound | No voice or music. Use quiet Mixkit transition effects. Deliver a sound mix and a silent version with the same picture. |
| Claims | Do not claim Store approval, signing, or purchase availability. |

## Sequence rules

- Keep the steps in order and use the labels **STEP 1**, **STEP 2**, and **STEP 3**.
- Do not show the composition output before the result scene.
- Keep UI text and file names readable. Do not enlarge the 1215 x 642 still captures beyond native size.
- Do not draw or replace Explorer controls, app controls, files, pointers, or notifications. The step 2 still uses the captured pointer over the real command.
- Keep the output image fully visible after the motion settles.
- Record timing in the `storeTimeline` object in `video/src/SoftstageStoreTrailer.tsx`.

## Motion reference

Use the `crash-zoom-punch` camera card as a reference for the output reveal. The edit uses a custom 6-frame ease-in from 90% to 108%, a 6-frame recovery to 102%, then a long hold. This does not copy the card demo continuously. It uses only the real Softstage output image as the target.
