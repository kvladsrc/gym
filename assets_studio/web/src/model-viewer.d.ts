// <model-viewer> is a custom element registered by @google/model-viewer.
// Aliased: inside `namespace JSX` the bare name would resolve to the deprecated JSX.HTMLAttributes.
import type { HTMLAttributes as ElementAttributes } from "preact";

declare module "preact" {
  namespace JSX {
    interface IntrinsicElements {
      "model-viewer": ElementAttributes<HTMLElement> & {
        src: string;
        "camera-controls"?: boolean;
        "auto-rotate"?: boolean;
        "shadow-intensity"?: string;
        exposure?: string;
        loading?: "auto" | "lazy" | "eager";
        "interaction-prompt"?: "auto" | "none";
      };
    }
  }
}
