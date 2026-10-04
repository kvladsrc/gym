// An image at its real size: fitted to the screen, or one image pixel per
// screen pixel (100 %), or twice that with sharp pixels (200 %, for pixel art).
// The checkerboard behind it shows transparency.
import { useEffect, useRef, useState } from "preact/hooks";

import type { Asset } from "../api/client";
import { assetName } from "../format";
import { t } from "../i18n";

type Zoom = "fit" | 1 | 2;

const ZOOMS: readonly [Zoom, () => string][] = [
  ["fit", () => t("viewer.fit")],
  [1, () => "100 %"],
  [2, () => "200 %"],
];

/** CSS width of the image at a zoom: image pixels over device pixels, so that
 * 100 % is one image pixel per screen pixel on high-density screens too. */
export function displayWidth(natural: number, zoom: 1 | 2, pixelRatio: number) {
  return (natural * zoom) / pixelRatio;
}

export function ImageViewer({
  asset,
  onClose,
}: {
  asset: Asset;
  onClose: () => void;
}) {
  const [zoom, setZoom] = useState<Zoom>("fit");
  const [natural, setNatural] = useState<[number, number] | null>(null);
  const close = useRef<HTMLButtonElement>(null);
  const dialog = useRef<HTMLDivElement>(null);
  // The latest callback, without re-running the effect below: the page
  // re-renders on every job update, with a new function each time.
  const closing = useRef(onClose);
  closing.current = onClose;

  useEffect(() => {
    const opener = document.activeElement as HTMLElement | null;
    close.current?.focus();
    // The page behind does not scroll while the viewer is open.
    const overflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") closing.current();
      if (event.key === "Tab" && dialog.current) {
        // Keep focus inside the viewer.
        const focusable = [
          ...dialog.current.querySelectorAll<HTMLElement>("button"),
        ];
        const first = focusable[0];
        const last = focusable[focusable.length - 1];
        if (event.shiftKey && document.activeElement === first) {
          event.preventDefault();
          last?.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
          event.preventDefault();
          first?.focus();
        }
      }
    };
    window.addEventListener("keydown", onKey);
    return () => {
      window.removeEventListener("keydown", onKey);
      document.body.style.overflow = overflow;
      opener?.focus(); // back to the image that opened it
    };
  }, []);

  const style =
    zoom === "fit" || !natural
      ? undefined
      : {
          width: `${displayWidth(natural[0], zoom, window.devicePixelRatio || 1)}px`,
          maxWidth: "none",
          maxHeight: "none",
        };

  return (
    <div
      ref={dialog}
      class="viewer"
      role="dialog"
      aria-modal="true"
      aria-label={assetName(asset)}
    >
      <div class="viewer-bar">
        <span class="viewer-title">{assetName(asset)}</span>
        {natural && (
          <span class="muted">
            {natural[0]} × {natural[1]}
          </span>
        )}
        <span class="spacer" />
        {ZOOMS.map(([value, label]) => (
          <button
            key={String(value)}
            class={`button small${zoom === value ? " active" : ""}`}
            aria-pressed={zoom === value}
            onClick={() => setZoom(value)}
          >
            {label()}
          </button>
        ))}
        <button ref={close} class="button small" onClick={onClose}>
          {t("viewer.close")}
        </button>
      </div>
      <div
        class={`viewer-body${zoom === "fit" ? " fit" : ""}`}
        onClick={(event) => {
          // A click beside the image closes; on the image it does nothing.
          if (event.target === event.currentTarget) onClose();
        }}
      >
        <img
          src={asset.file_url}
          alt={assetName(asset)}
          class={zoom === 2 ? "pixelated" : ""}
          style={style}
          onLoad={(event) =>
            setNatural([
              event.currentTarget.naturalWidth,
              event.currentTarget.naturalHeight,
            ])
          }
        />
      </div>
    </div>
  );
}
