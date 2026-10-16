import { MyComposition } from "./Composition";
import {
  SoftstageStoreScreenshotCompositions,
  SoftstageStoreTrailerComposition,
} from "./SoftstageStoreTrailer";

export const RemotionRoot: React.FC = () => {
  return (
    <>
      <MyComposition />
      <SoftstageStoreTrailerComposition />
      <SoftstageStoreScreenshotCompositions />
    </>
  );
};
