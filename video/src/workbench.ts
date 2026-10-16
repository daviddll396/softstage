import React from "react";
import { SoftstageStoreTrailerR2, storeR2Timeline } from "./SoftstageStoreTrailerR2";

const totalFrames = storeR2Timeline.totalFrames;
const OriginalTrailer: React.FC = () =>
  React.createElement(SoftstageStoreTrailerR2, { sfx: true });

// Keep this as one honest clip. The film is rendered from one composition;
// its scene timing and internal sound cues are not separate workbench clips.
export const WORKBENCH = {
  name: "Softstage Store trailer R2",
  fps: 30,
  width: 1920,
  height: 1080,
  total: totalFrames,
  background: "#121216",
  revision: "r2",
  shots: [
    {
      id: "softstage-store-trailer-r2",
      label: "Full trailer R2",
      from: 0,
      duration: totalFrames,
      component: SoftstageStoreTrailerR2,
      props: { sfx: true },
      schema: [
        { type: "boolean", key: "sfx", label: "Transition sound effects", default: true },
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
