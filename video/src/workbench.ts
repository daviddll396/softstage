import React from "react";
import { SoftstageStoreTrailer, storeTimeline } from "./SoftstageStoreTrailer";

const totalFrames = storeTimeline.closing.from + storeTimeline.closing.duration;
const OriginalTrailer: React.FC = () =>
  React.createElement(SoftstageStoreTrailer, { sfx: true });

// Keep this as one honest clip. The film is rendered from one composition;
// its scene timing and internal sound cues are not separate workbench clips.
export const WORKBENCH = {
  name: "Softstage Store trailer",
  fps: 30,
  width: 1920,
  height: 1080,
  total: totalFrames,
  background: "#121216",
  revision: "r1",
  shots: [
    {
      id: "softstage-store-trailer",
      label: "Full trailer",
      from: 0,
      duration: totalFrames,
      component: SoftstageStoreTrailer,
      props: { sfx: true },
      schema: [
        {
          type: "boolean",
          key: "sfx",
          label: "Transition sound effects",
          default: true,
        },
      ],
    },
  ],
  transitions: [],
  captions: [],
  overlays: [],
  sfx: [],
  bgm: [],
  order: [],
  original: OriginalTrailer,
};
