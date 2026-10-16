import { MyComposition } from "./Composition";
import {
  SoftstageStoreScreenshotCompositions,
  SoftstageStoreTrailerComposition,
} from "./SoftstageStoreTrailer";
import { SoftstageStoreTrailerR2Composition } from "./SoftstageStoreTrailerR2";

export const RemotionRoot: React.FC = () => {
  return (
    <>
      <MyComposition />
      <SoftstageStoreTrailerComposition />
      <SoftstageStoreTrailerR2Composition />
      <SoftstageStoreScreenshotCompositions />
    </>
  );
};
