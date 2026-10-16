# Backdrop launch film

This folder contains the editable Remotion source for the 33-second Backdrop
launch film. It uses generated sample screens and real screenshots of the
Windows application. It does not show an Explorer menu or claim that Explorer
visibility was verified.

## Render

Use Node.js, then install the pinned dependencies and start the preview:

```powershell
npm ci
npm run dev
```

For the licensed transition sounds, download these Mixkit files into
`public/audio/` before rendering:

- `swoosh-quick.mp3` — <https://assets.mixkit.co/active_storage/sfx/166/166-preview.mp3>
- `transition-soft.mp3` — <https://assets.mixkit.co/active_storage/sfx/2608/2608-preview.mp3>

They are covered by the [Mixkit Sound Effects Free License](https://mixkit.co/license/modal/sfxFree/).
The MP3 files are ignored by Git because the license allows use in a video but
does not allow redistributing the sound files on their own. The rendered MP4
includes the low-volume cues.

Run the type and lint checks, then render the 1,920 × 1,080, 30 fps film:

```powershell
npm run lint
npx remotion render src/index.ts BackdropLaunch out/backdrop-launch.mp4 --codec h264
```

The checked cut is 990 frames. The source composition and still assets remain
editable in this project.

## Softstage Store trailer, revision 1

The Store trailer source is `src/SoftstageStoreTrailer.tsx`. Its 24-second
timeline is `storeTimeline` in that file. The approved captures, storyboard,
source record, and exports are in `../assets/store/r1/`.

Install the pinned dependencies and render the source movie:

```powershell
npm ci
npm run build
npx remotion render src/index.ts SoftstageStoreTrailer out/softstage-store-trailer-source.mp4 --codec=h264 --crf=12 --audio-codec=aac --audio-bitrate=384k --pixel-format=yuv420p --gop=15 --concurrency=4 --overwrite
```

Use FFmpeg to make the Store encode. Run this from the `video` folder:

```powershell
New-Item -ItemType Directory -Force ../artifacts/store-media | Out-Null
ffmpeg -i out/softstage-store-trailer-source.mp4 -map 0:v:0 -map 0:a:0 -vf "scale=in_range=full:out_range=tv,format=yuv420p" -r 30 -c:v libx264 -preset slow -profile:v high -level:v 4.2 -tag:v avc1 -b:v 50M -minrate 50M -maxrate 50M -bufsize 50M -g 15 -keyint_min 15 -sc_threshold 0 -bf 2 -x264-params "open-gop=0:cabac=1:scenecut=0:keyint=15:min-keyint=15:nal-hrd=cbr:filler=1" -color_primaries bt709 -color_trc bt709 -colorspace bt709 -color_range tv -c:a copy -movflags +faststart -use_editlist 0 ../artifacts/store-media/softstage-store-trailer.mp4
ffmpeg -i ../artifacts/store-media/softstage-store-trailer.mp4 -map 0:v:0 -c:v copy -an -movflags +faststart -use_editlist 0 ../artifacts/store-media/softstage-store-trailer-silent.mp4
```

The two large MP4 files go in the ignored `../artifacts/store-media/` folder.
The silent file has the same picture and timing. It has no audio track. The
subtitle file is `../assets/store/r1/exports/softstage-store-trailer.en.vtt`.
The PNG exports are rendered from the four Store screenshot compositions in
`src/SoftstageStoreTrailer.tsx`. The text-free hero art is a downscaled real
Softstage composition output, not a mock interface.

The master uses the Mixkit transition effect listed above. The MP3 file stays
ignored by Git because its license does not allow standalone redistribution.
The exported video may include the effect.
