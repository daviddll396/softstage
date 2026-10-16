import React from "react";
import {
  AbsoluteFill,
  Audio,
  Composition,
  Easing,
  Img,
  interpolate,
  Sequence,
  staticFile,
  useCurrentFrame,
} from "remotion";

type TrailerProps = { sfx: boolean };
type StoreShot = "selected" | "command" | "created" | "composition";

const fps = 30;
export const storeTimeline = {
  opening: { from: 0, duration: 60 },
  step1: { from: 60, duration: 150 },
  step2: { from: 210, duration: 150 },
  step3: { from: 360, duration: 150 },
  output: { from: 510, duration: 120 },
  closing: { from: 630, duration: 90 },
} as const;
const totalFrames = storeTimeline.closing.from + storeTimeline.closing.duration;
const colors = {
  canvas: "#121216",
  canvasRaised: "#191820",
  text: "#f5f3fa",
  muted: "#a6a2ae",
  lilac: "#cbbfff",
  lilacSoft: "rgba(203,191,255,0.16)",
  edge: "rgba(235,230,255,0.12)",
};
const font = '"Softstage Google Sans", "Segoe UI", sans-serif';
const ease = Easing.bezier(0.22, 1, 0.36, 1);
const media = (name: string) => staticFile(`media/store-r1/${name}`);

const FontFaces = () => (
  <style>{`
    @font-face {
      font-family: "Softstage Google Sans";
      src: url("${media("GoogleSansFlex-Regular.ttf")}") format("truetype");
      font-style: normal;
      font-weight: 400;
    }
    @font-face {
      font-family: "Softstage Google Sans";
      src: url("${media("GoogleSansFlex-Bold.ttf")}") format("truetype");
      font-style: normal;
      font-weight: 700;
    }
  `}</style>
);

const Canvas = ({ children }: { children: React.ReactNode }) => (
  <AbsoluteFill
    style={{
      color: colors.text,
      fontFamily: font,
      background: `radial-gradient(ellipse at 68% 38%, rgba(82,68,118,0.18), transparent 45%), ${colors.canvas}`,
      overflow: "hidden",
    }}
  >
    <FontFaces />
    {children}
  </AbsoluteFill>
);

const clamp = (frame: number, input: [number, number], output: [number, number]) =>
  interpolate(frame, input, output, {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
    easing: ease,
  });

const Wordmark = ({ size = 70 }: { size?: number }) => (
  <div
    style={{
      color: colors.text,
      fontSize: size,
      lineHeight: 0.98,
      fontWeight: 700,
      letterSpacing: -2.8,
      whiteSpace: "nowrap",
    }}
  >
    Softstage
  </div>
);

const BrandScene: React.FC<{ closing?: boolean }> = ({ closing = false }) => {
  const frame = useCurrentFrame();
  const enter = clamp(frame, [0, 15], [0, 1]);
  const shine = clamp(frame, [7, 21], [-130, 150]);
  const settle = closing ? 0 : clamp(frame, [0, 45], [0, -7]);

  return (
    <Canvas>
      <AbsoluteFill
        style={{
          background:
            "radial-gradient(ellipse at 50% 46%, rgba(203,191,255,0.12), transparent 37%), linear-gradient(145deg,#121216 25%,#191722 100%)",
        }}
      />
      <div
        style={{
          position: "absolute",
          left: 0,
          right: 0,
          top: 0,
          bottom: 0,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          opacity: enter,
          transform: `translateY(${settle}px)`,
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 30 }}>
          <div
            style={{
              position: "relative",
              width: 138,
              height: 138,
              overflow: "hidden",
              borderRadius: 28,
              boxShadow: "0 22px 74px rgba(0,0,0,0.36)",
              flex: "0 0 auto",
            }}
          >
            <Img
              src={media("softstage-icon.png")}
              style={{ width: "100%", height: "100%", display: "block" }}
            />
            <div
              style={{
                position: "absolute",
                top: -20,
                bottom: -20,
                left: 0,
                width: 42,
                opacity: 0.58,
                transform: `translateX(${shine}px) skewX(-17deg)`,
                background:
                  "linear-gradient(90deg, transparent, rgba(255,255,255,0.62), transparent)",
                mixBlendMode: "screen",
              }}
            />
          </div>
          <Wordmark />
        </div>
      </div>
      {closing && (
        <div
          style={{
            position: "absolute",
            bottom: 72,
            left: 0,
            right: 0,
            textAlign: "center",
            color: colors.muted,
            fontSize: 20,
            letterSpacing: 0.3,
            opacity: clamp(frame, [12, 25], [0, 1]),
          }}
        >
          Select. Right-click. Compose.
        </div>
      )}
    </Canvas>
  );
};

const StepScene: React.FC<{
  number: string;
  headline: string;
  detail: string;
  image: string;
  nativeSize?: boolean;
}> = ({
  number,
  headline,
  detail,
  image,
  nativeSize = false,
}) => {
  const frame = useCurrentFrame();
  const fade = clamp(frame, [0, 14], [0, 1]);
  const rise = clamp(frame, [0, 18], [18, 0]);
  const numberLabel = `STEP ${number}`;
  return (
    <Canvas>
      <div
        style={{
          position: "absolute",
          left: 104,
          top: 0,
          bottom: 0,
          width: 265,
          display: "flex",
          justifyContent: "center",
          flexDirection: "column",
          opacity: fade,
          transform: `translateY(${rise}px)`,
        }}
      >
        <div
          style={{
            fontSize: 15,
            fontWeight: 700,
            letterSpacing: 2.4,
            color: colors.lilac,
            marginBottom: 26,
          }}
        >
          {numberLabel}
        </div>
        <div
          style={{
            width: 42,
            height: 2,
            background: colors.lilac,
            marginBottom: 28,
            opacity: 0.92,
          }}
        />
        <div
          style={{
            fontSize: 36,
            lineHeight: 1.13,
            letterSpacing: -1.1,
            fontWeight: 700,
            marginBottom: 20,
          }}
        >
          {headline}
        </div>
        <div
          style={{
            color: colors.muted,
            fontSize: 18,
            lineHeight: 1.48,
            maxWidth: 250,
          }}
        >
          {detail}
        </div>
      </div>
      <div
        style={{
          position: "absolute",
          left: 400,
          top: 44,
          width: 1474,
          height: 992,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          overflow: "hidden",
          border: `1px solid ${colors.edge}`,
          background: "#09090c",
          boxShadow: "0 18px 64px rgba(0,0,0,0.28)",
          opacity: fade,
          transform: `translateY(${rise * 0.45}px)`,
        }}
      >
        <Img
          src={media(image)}
          style={{
            width: nativeSize ? undefined : "100%",
            height: nativeSize ? undefined : "100%",
            maxWidth: "100%",
            maxHeight: "100%",
            objectFit: "contain",
          }}
        />
      </div>
    </Canvas>
  );
};

const OutputScene = () => {
  const frame = useCurrentFrame();
  const punch = interpolate(frame, [0, 6], [0.9, 1.08], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
    easing: Easing.in(Easing.quad),
  });
  const recovery = interpolate(frame, [6, 12], [1.08, 1.02], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
    easing: Easing.out(Easing.quad),
  });
  const reveal = frame <= 6 ? punch : recovery;
  const lift = clamp(frame, [0, 8], [16, 0]);
  return (
    <Canvas>
      <div
        style={{
          position: "absolute",
          left: 210,
          right: 210,
          top: 70,
          height: 810,
          display: "flex",
          justifyContent: "center",
          alignItems: "center",
          opacity: 1,
          transform: `translateY(${lift}px)`,
        }}
      >
        <div
          style={{
            width: 1440,
            height: 810,
          overflow: "visible",
          }}
        >
          <Img
            src={media("created-composition.png")}
          style={{
            display: "block",
            width: "100%",
            height: "100%",
            border: `1px solid ${colors.edge}`,
            boxShadow: "0 28px 88px rgba(0,0,0,0.36)",
            transform: `scale(${reveal})`,
          }}
          />
        </div>
      </div>
      <div
        style={{
          position: "absolute",
          left: 0,
          right: 0,
          bottom: 74,
          textAlign: "center",
          fontSize: 32,
          fontWeight: 700,
          letterSpacing: -0.8,
          opacity: clamp(frame, [7, 18], [0, 1]),
        }}
      >
        Your PNG is ready.
      </div>
    </Canvas>
  );
};

export const SoftstageStoreTrailer: React.FC<TrailerProps> = ({ sfx }) => (
  <AbsoluteFill>
    <Sequence from={storeTimeline.opening.from} durationInFrames={storeTimeline.opening.duration}>
      <BrandScene />
    </Sequence>
    <Sequence from={storeTimeline.step1.from} durationInFrames={storeTimeline.step1.duration}>
      <StepScene
        number="1"
        headline="Select three images"
        detail="Choose the source images together in File Explorer."
        image="select-images.png"
        nativeSize
      />
    </Sequence>
    <Sequence from={storeTimeline.step2.from} durationInFrames={storeTimeline.step2.duration}>
      <StepScene
        number="2"
        headline="Create with Softstage"
        detail="Right-click the selection and choose the highlighted command."
        image="context-menu-pointer.png"
        nativeSize
      />
    </Sequence>
    <Sequence from={storeTimeline.step3.from} durationInFrames={storeTimeline.step3.duration}>
      <StepScene
        number="3"
        headline="Your PNG is ready"
        detail="The new composition appears beside the original images."
        image="created-files.png"
        nativeSize
      />
    </Sequence>
    <Sequence from={storeTimeline.output.from} durationInFrames={storeTimeline.output.duration}>
      <OutputScene />
    </Sequence>
    <Sequence from={storeTimeline.closing.from} durationInFrames={storeTimeline.closing.duration}>
      <BrandScene closing />
    </Sequence>
    {sfx && (
      <>
        {[
          storeTimeline.step1.from,
          storeTimeline.step2.from,
          storeTimeline.step3.from,
          storeTimeline.closing.from,
        ].map((from) => (
          <Sequence key={from} from={from} durationInFrames={42}>
            <Audio src={staticFile("audio/swoosh-quick.mp3")} volume={0.12} />
          </Sequence>
        ))}
      </>
    )}
  </AbsoluteFill>
);

const screenshotFiles: Record<StoreShot, { file: string; label: string; title: string }> = {
  selected: {
    file: "select-images.png",
    label: "STEP 1",
    title: "Select three images",
  },
  command: {
    file: "context-menu-pointer.png",
    label: "STEP 2",
    title: "Create a composition",
  },
  created: {
    file: "created-files.png",
    label: "STEP 3",
    title: "Your PNG is ready",
  },
  composition: {
    file: "created-composition.png",
    label: "THE RESULT",
    title: "One PNG. Ready to share.",
  },
};

export const StoreScreenshot: React.FC<{ shot: StoreShot }> = ({ shot }) => {
  const item = screenshotFiles[shot];
  const wide = shot === "composition";
  return (
    <Canvas>
      <div
        style={{
          position: "absolute",
          left: 110,
          top: 88,
          right: 110,
          height: 72,
          display: "flex",
          alignItems: "center",
          gap: 20,
        }}
      >
        <div
          style={{
            color: colors.lilac,
            fontSize: 15,
            fontWeight: 700,
            letterSpacing: 2,
          }}
        >
          {item.label}
        </div>
        <div style={{ width: 1, height: 24, background: colors.edge }} />
        <div style={{ fontSize: 28, fontWeight: 700, letterSpacing: -0.7 }}>
          {item.title}
        </div>
      </div>
      {wide ? (
        <div
          style={{
            position: "absolute",
            left: 110,
            top: 190,
            width: 1700,
            height: 820,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            overflow: "hidden",
            border: `1px solid ${colors.edge}`,
          }}
        >
          <Img
            src={media(item.file)}
            style={{ width: "100%", height: "100%", objectFit: "contain" }}
          />
        </div>
      ) : (
        <div
          style={{
            position: "absolute",
            left: 340,
            top: 185,
            width: 1240,
            height: 810,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            overflow: "hidden",
            border: `1px solid ${colors.edge}`,
            background: "#09090c",
            boxShadow: "0 18px 64px rgba(0,0,0,0.28)",
          }}
        >
          <Img
            src={media(item.file)}
            style={{
              maxWidth: "100%",
              maxHeight: "100%",
              objectFit: "contain",
            }}
          />
        </div>
      )}
      <div
        style={{
          position: "absolute",
          left: 0,
          right: 0,
          bottom: 44,
          textAlign: "center",
          color: colors.muted,
          fontSize: 16,
          letterSpacing: 0.2,
        }}
      >
        Softstage for Windows
      </div>
    </Canvas>
  );
};

export const SoftstageStoreTrailerComposition = () => (
  <Composition
    id="SoftstageStoreTrailer"
    component={SoftstageStoreTrailer}
    durationInFrames={totalFrames}
    fps={fps}
    width={1920}
    height={1080}
    defaultProps={{ sfx: true }}
  />
);

export const SoftstageStoreScreenshotCompositions = () => (
  <>
    {([
      ["StoreSelectScreenshot", "selected"],
      ["StoreCommandScreenshot", "command"],
      ["StoreCreatedScreenshot", "created"],
      ["StoreCompositionScreenshot", "composition"],
    ] as const).map(([id, shot]) => (
      <Composition
        key={id}
        id={id}
        component={StoreScreenshot}
        durationInFrames={1}
        fps={fps}
        width={1920}
        height={1080}
        defaultProps={{ shot }}
      />
    ))}
  </>
);

