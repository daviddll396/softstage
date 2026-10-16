import React from "react";
import {
  AbsoluteFill,
  Audio,
  Composition,
  Easing,
  interpolate,
  OffthreadVideo,
  Sequence,
  staticFile,
  useCurrentFrame,
} from "remotion";

const fps = 30;
const media = (name: string) => staticFile(`media/store-r2/${name}`);
const brandMedia = (name: string) => staticFile(`media/store-r1/${name}`);
const text = "#f5f3fa";
const muted = "#b7b3c1";
const lilac = "#cbbfff";
const ease = Easing.bezier(0.22, 1, 0.36, 1);

type Clip = {
  id: string;
  source: "explorer" | "command";
  startSeconds: number;
  endSeconds: number;
  playbackRate: number;
  step: "1" | "2" | "3";
  title: string;
  detail?: string;
};

export const storeR2Clips: Clip[] = [
  {
    id: "select-images",
    source: "explorer",
    startSeconds: 11.5,
    endSeconds: 15.5,
    playbackRate: 1,
    step: "1",
    title: "Select three images",
  },
  {
    id: "right-click",
    source: "explorer",
    startSeconds: 15.5,
    endSeconds: 25,
    playbackRate: 3,
    step: "2",
    title: "Right-click the selection",
  },
  {
    id: "show-more-options",
    source: "explorer",
    startSeconds: 25,
    endSeconds: 38.5,
    playbackRate: 3,
    step: "2",
    title: "Open the Softstage command",
  },
  {
    id: "command-click",
    source: "command",
    startSeconds: 58.5,
    endSeconds: 61.4,
    playbackRate: 1,
    step: "2",
    title: "Create with Softstage",
  },
  {
    id: "png-created",
    source: "explorer",
    startSeconds: 51.5,
    endSeconds: 81.5,
    playbackRate: 5.5,
    step: "2",
    title: "The new PNG appears",
  },
  {
    id: "open-png",
    source: "explorer",
    startSeconds: 81.5,
    endSeconds: 86.8,
    playbackRate: 1.5,
    step: "3",
    title: "Open the new composition",
  },
];

const clipDuration = (clip: Clip) => {
  const durationFrames =
    ((clip.endSeconds - clip.startSeconds) * fps) / clip.playbackRate;
  return Math.ceil(Number(durationFrames.toFixed(6)));
};
const clipFrames = storeR2Clips.map(clipDuration);
const openingFrames = 46;
const photosFrames = 120;
const photosEaseFrames = 16;
const closingFrames = 60;
const clipStartFrames = storeR2Clips.reduce<number[]>(
  (starts, _clip, index) => [
    ...starts,
    index === 0 ? openingFrames : starts[index - 1] + clipFrames[index - 1],
  ],
  [],
);
const photosStart = clipStartFrames[5] + clipFrames[5];
const photosHoldStart = photosStart + photosEaseFrames;
const closingStart = photosStart + photosFrames;
export const storeR2Timeline = {
  fps,
  width: 1920,
  height: 1080,
  opening: { from: 0, duration: openingFrames },
  clips: storeR2Clips.map((clip, index) => ({
    id: clip.id,
    from: clipStartFrames[index],
    duration: clipFrames[index],
  })),
  photos: { from: photosStart, duration: photosFrames },
  closing: { from: closingStart, duration: closingFrames },
  totalFrames: closingStart + closingFrames,
} as const;

const Canvas: React.FC<{ children?: React.ReactNode }> = ({ children }) => (
  <AbsoluteFill
    style={{
      color: text,
      fontFamily: '"Softstage Google Sans", "Segoe UI", sans-serif',
      background:
        "radial-gradient(ellipse at 65% 38%, rgba(88,74,126,0.22), transparent 45%), #121216",
      overflow: "hidden",
    }}
  >
    <style>{`
      @font-face {
        font-family: "Softstage Google Sans";
        src: url("${brandMedia("GoogleSansFlex-Regular.ttf")}") format("truetype");
        font-style: normal;
        font-weight: 400;
      }
      @font-face {
        font-family: "Softstage Google Sans";
        src: url("${brandMedia("GoogleSansFlex-Bold.ttf")}") format("truetype");
        font-style: normal;
        font-weight: 700;
      }
    `}</style>
    {children}
  </AbsoluteFill>
);

const SoftstageMark: React.FC<{ closing?: boolean }> = ({ closing = false }) => {
  const frame = useCurrentFrame();
  const entrance = interpolate(frame, [0, 16], [0, 1], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
    easing: ease,
  });
  const shine = interpolate(frame, [4, 23], [-150, 170], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
    easing: ease,
  });

  return (
    <Canvas>
      <AbsoluteFill
        style={{
          background:
            "radial-gradient(ellipse at 50% 46%, rgba(203,191,255,0.13), transparent 36%), linear-gradient(145deg,#121216 24%,#191722 100%)",
        }}
      />
      <div
        style={{
          position: "absolute",
          inset: 0,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          opacity: entrance,
          transform: `translateY(${closing ? 0 : 5}px)`,
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 26 }}>
          <div
            style={{
              position: "relative",
              width: 108,
              height: 108,
              borderRadius: 23,
              overflow: "hidden",
              flex: "0 0 auto",
              boxShadow: "0 20px 68px rgba(0,0,0,0.32)",
            }}
          >
            <img
              src={brandMedia("softstage-icon.png")}
              style={{ width: "100%", height: "100%", display: "block" }}
            />
            <div
              style={{
                position: "absolute",
                inset: "-18px auto -18px 0",
                width: 40,
                opacity: 0.5,
                transform: `translateX(${shine}px) skewX(-17deg)`,
                background:
                  "linear-gradient(90deg,transparent,rgba(255,255,255,.66),transparent)",
                mixBlendMode: "screen",
              }}
            />
          </div>
          <div
            style={{
              fontSize: 74,
              lineHeight: 0.98,
              fontWeight: 700,
              letterSpacing: -3.2,
              whiteSpace: "nowrap",
            }}
          >
            Softstage
          </div>
        </div>
      </div>
      {closing && (
        <div
          style={{
            position: "absolute",
            left: 0,
            right: 0,
            bottom: 78,
            color: muted,
            fontSize: 19,
            letterSpacing: 0.35,
            textAlign: "center",
            opacity: interpolate(frame, [8, 20], [0, 1], {
              extrapolateLeft: "clamp",
              extrapolateRight: "clamp",
            }),
          }}
        >
          Select. Right-click. Compose.
        </div>
      )}
    </Canvas>
  );
};

const Caption: React.FC<{ step: Clip["step"]; title: string; frame: number }> = ({
  step,
  title,
  frame,
}) => {
  const opacity = interpolate(frame, [0, 10], [0, 1], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
    easing: ease,
  });
  const lift = interpolate(frame, [0, 12], [10, 0], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
    easing: ease,
  });

  return (
    <div
      style={{
        position: "absolute",
        left: 56,
        bottom: 48,
        display: "flex",
        alignItems: "center",
        gap: 16,
        padding: "15px 22px 15px 15px",
        border: "1px solid rgba(232,225,255,0.14)",
        borderRadius: 18,
        background: "rgba(18,18,22,0.88)",
        boxShadow: "0 14px 38px rgba(0,0,0,0.24)",
        opacity,
        transform: `translateY(${lift}px)`,
      }}
    >
      <div
        style={{
          width: 40,
          height: 40,
          display: "grid",
          placeItems: "center",
          borderRadius: 12,
          color: "#16131f",
          background: lilac,
          fontSize: 15,
          fontWeight: 700,
          letterSpacing: 0.4,
        }}
      >
        {step}
      </div>
      <div>
        <div
          style={{
            color: lilac,
            fontSize: 12,
            fontWeight: 700,
            letterSpacing: 1.7,
            marginBottom: 3,
          }}
        >
          STEP {step}
        </div>
        <div style={{ fontSize: 20, fontWeight: 700, letterSpacing: -0.25 }}>
          {title}
        </div>
      </div>
    </div>
  );
};

const CommandActionClip: React.FC<{ clip: Clip }> = ({ clip }) => {
  const frame = useCurrentFrame();
  const enter = interpolate(frame, [0, 13], [0, 1], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
    easing: ease,
  });
  return (
    <Canvas>
      <OffthreadVideo
        src={media("command-closeup.mp4")}
        startFrom={Math.round(clip.startSeconds * fps)}
        endAt={Math.round(clip.endSeconds * fps)}
        playbackRate={clip.playbackRate}
        muted
        style={{
          width: "100%",
          height: "100%",
          display: "block",
          transform: "scale(2.2)",
          transformOrigin: "450px 370px",
        }}
      />
      <div
        style={{
          position: "absolute",
          left: 1085,
          top: 0,
          bottom: 0,
          width: 720,
          display: "flex",
          flexDirection: "column",
          justifyContent: "center",
          opacity: enter,
          transform: `translateY(${interpolate(frame, [0, 16], [12, 0], {
            extrapolateLeft: "clamp",
            extrapolateRight: "clamp",
            easing: ease,
          })}px)`,
        }}
      >
        <div
          style={{
            color: lilac,
            fontSize: 14,
            fontWeight: 700,
            letterSpacing: 2.2,
            marginBottom: 18,
          }}
        >
          STEP 2
        </div>
        <div
          style={{
            maxWidth: 610,
            color: text,
            fontSize: 42,
            lineHeight: 1.12,
            fontWeight: 700,
            letterSpacing: -1.2,
          }}
        >
          Create with Softstage
        </div>
        <div
          style={{
            width: 54,
            height: 2,
            marginTop: 28,
            background: lilac,
          }}
        />
      </div>
    </Canvas>
  );
};

const ExplorerClip: React.FC<{ clip: Clip }> = ({ clip }) => {
  const frame = useCurrentFrame();
  const isOutput = clip.id === "png-created";
  const isOpeningPng = clip.id === "open-png";
  const isCreatedFolder = isOutput || clip.id === "open-png";
  const explorerSource = isCreatedFolder
    ? "explorer-created.mp4"
    : "explorer-focused.mp4";
  const baseScale = isOutput || isOpeningPng ? 1 : 1.12;
  const outputAt = isOutput
    ? ((66 - clip.startSeconds) * fps) / clip.playbackRate
    : 0;
  const zoomStart = outputAt + 10;
  const zoomIn = interpolate(frame, [zoomStart, zoomStart + 25], [baseScale, 2.75], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
    easing: ease,
  });
  const zoomOut = interpolate(frame, [zoomStart + 45, zoomStart + 72], [2.75, baseScale], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
    easing: ease,
  });
  const cameraScale = isOutput
    ? frame < zoomStart + 45
      ? zoomIn
      : zoomOut
    : baseScale;
  const zoomProgress = isOutput
    ? interpolate(
        frame,
        [zoomStart, zoomStart + 25, zoomStart + 45, zoomStart + 72],
        [0, 1, 1, 0],
        {
          extrapolateLeft: "clamp",
          extrapolateRight: "clamp",
          easing: ease,
        },
      )
    : 0;
  const cameraTranslateX = -672 * zoomProgress;
  const cameraTranslateY = 236 * zoomProgress;
  const cameraOrigin = isOutput ? "1632px 304px" : "0px 0px";
  const captionOpacity =
    clip.id === "png-created"
      ? interpolate(frame, [0, 10, zoomStart + 12, zoomStart + 34], [0, 1, 1, 0], {
          extrapolateLeft: "clamp",
          extrapolateRight: "clamp",
          easing: ease,
        })
      : 1;

  return (
    <Canvas>
      <div
        style={{
          position: "absolute",
          inset: 0,
          overflow: "hidden",
          background: "#0c0c10",
        }}
      >
        <OffthreadVideo
          src={media(explorerSource)}
          startFrom={Math.round(clip.startSeconds * fps)}
          endAt={Math.round(clip.endSeconds * fps)}
          playbackRate={clip.playbackRate}
          muted
          style={{
            width: "100%",
            height: "100%",
            objectFit: "cover",
            display: "block",
            transform: isOutput
              ? `translate(${cameraTranslateX}px, ${cameraTranslateY}px) scale(${cameraScale})`
              : `translateY(${isOpeningPng ? 0 : 16}px) scale(${cameraScale})`,
            transformOrigin: cameraOrigin,
          }}
        />
      </div>
      <div style={{ opacity: captionOpacity }}>
        <Caption step={clip.step} title={clip.title} frame={frame} />
      </div>
    </Canvas>
  );
};

const PhotosTransition: React.FC = () => {
  const frame = useCurrentFrame();
  const scale = interpolate(frame, [0, photosEaseFrames - 1], [1.045, 1], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
    easing: ease,
  });
  return (
    <Canvas>
      <OffthreadVideo
        src={media("photos-window.mp4")}
        startFrom={0}
        endAt={photosEaseFrames}
        muted
        style={{
          width: "100%",
          height: "100%",
          objectFit: "cover",
          display: "block",
          transform: `scale(${scale})`,
        }}
      />
      <Caption step="3" title="Open the new composition" frame={frame} />
    </Canvas>
  );
};

const PhotosHold: React.FC = () => {
  const frame = useCurrentFrame();
  return (
    <Canvas>
      <OffthreadVideo
        src={media("photos-window.mp4")}
        startFrom={photosEaseFrames}
        endAt={photosFrames}
        muted
        style={{ width: "100%", height: "100%", display: "block" }}
      />
      <Caption
        step="3"
        title="Open the new composition"
        frame={frame + photosEaseFrames}
      />
    </Canvas>
  );
};

export const SoftstageStoreTrailerR2: React.FC<{ sfx: boolean }> = ({ sfx }) => (
  <AbsoluteFill>
    <Sequence from={storeR2Timeline.opening.from} durationInFrames={openingFrames}>
      <SoftstageMark />
    </Sequence>
    {storeR2Clips.map((clip, index) => (
      <Sequence
        key={clip.id}
        from={clipStartFrames[index]}
        durationInFrames={clipFrames[index]}
      >
        {clip.source === "command" ? (
          <CommandActionClip clip={clip} />
        ) : (
          <ExplorerClip clip={clip} />
        )}
      </Sequence>
    ))}
    <Sequence from={photosStart} durationInFrames={photosEaseFrames}>
      <PhotosTransition />
    </Sequence>
    <Sequence
      from={photosHoldStart}
      durationInFrames={photosFrames - photosEaseFrames}
    >
      <PhotosHold />
    </Sequence>
    <Sequence from={closingStart} durationInFrames={closingFrames}>
      <SoftstageMark closing />
    </Sequence>
    {sfx && (
      <>
        <Sequence from={2} durationInFrames={30}>
          <Audio src={staticFile("audio/swoosh-quick.mp3")} volume={0.08} />
        </Sequence>
        <Sequence from={photosStart} durationInFrames={28}>
          <Audio src={staticFile("audio/swoosh-quick.mp3")} volume={0.06} />
        </Sequence>
      </>
    )}
  </AbsoluteFill>
);

export const SoftstageStoreTrailerR2Composition = () => (
  <Composition
    id="SoftstageStoreTrailerR2"
    component={SoftstageStoreTrailerR2}
    durationInFrames={storeR2Timeline.totalFrames}
    fps={fps}
    width={1920}
    height={1080}
    defaultProps={{ sfx: true }}
  />
);
