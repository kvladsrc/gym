// Large preview: image, 3D model (model-viewer; FBX only described), audio (waveform + controls),
// video (native player) or text (with copying).
// The 3D and audio libraries are loaded on first use to keep the page light.
import { useEffect, useRef, useState } from "preact/hooks";

import type { Asset } from "../api/client";
import { assetName } from "../format";
import { cssColor, dark } from "../state/theme";
import { ImageViewer } from "./ImageViewer";
import { t } from "../i18n";

let modelViewer: Promise<unknown> | null = null;

type Material = {
  pbrMetallicRoughness: {
    setBaseColorFactor: (rgba: [number, number, number, number]) => void;
    setMetallicFactor: (value: number) => void;
    setRoughnessFactor: (value: number) => void;
  };
};

/** A shape without colour (Hunyuan3D: meta.color "none") comes with glTF's
 * default material, white and fully metallic: it renders as a flat white
 * silhouette. It is shown as grey matte clay instead, so the shape reads in
 * light and shade. */
export function clayIfShapeOnly(asset: Asset) {
  if (asset.meta.color !== "none") return undefined;
  return (event: Event) => {
    const model = (event.target as { model?: { materials: Material[] } }).model;
    for (const material of model?.materials ?? []) {
      material.pbrMetallicRoughness.setBaseColorFactor([0.62, 0.6, 0.57, 1]);
      material.pbrMetallicRoughness.setMetallicFactor(0);
      material.pbrMetallicRoughness.setRoughnessFactor(0.85);
    }
  };
}

/** Loads @google/model-viewer once; true when <model-viewer> is defined. */
export function useModelViewer(enabled = true) {
  const [ready, setReady] = useState(false);
  useEffect(() => {
    if (!enabled) return;
    modelViewer ??= import("@google/model-viewer");
    void modelViewer.then(() => setReady(true));
  }, [enabled]);
  return ready;
}

function MeshPreview({ asset }: { asset: Asset }) {
  const glb = asset.mime === "model/gltf-binary";
  const ready = useModelViewer(glb);
  // model-viewer shows glTF only; FBX (rigs, ADR-006) is for download.
  if (!glb) return <p class="hint">{t("preview.fbx")}</p>;
  if (!ready) return <span class="spinner" />;
  return (
    <model-viewer
      src={asset.file_url}
      camera-controls
      auto-rotate
      shadow-intensity="0.6"
      exposure="1.05"
      onLoad={clayIfShapeOnly(asset)}
    />
  );
}

function AudioPreview({ asset }: { asset: Asset }) {
  const container = useRef<HTMLDivElement>(null);
  const [playing, setPlaying] = useState(false);
  const player = useRef<{
    playPause: () => Promise<void>;
    destroy: () => void;
    setOptions: (options: Record<string, string>) => void;
  } | null>(null);

  useEffect(() => {
    let cancelled = false;
    void import("wavesurfer.js").then(({ default: WaveSurfer }) => {
      if (cancelled || !container.current) return;
      const wave = WaveSurfer.create({
        container: container.current,
        url: asset.file_url,
        height: 72,
        waveColor: cssColor("--wave"),
        progressColor: cssColor("--accent"),
        cursorColor: cssColor("--accent"),
        barWidth: 2,
        barGap: 1,
      });
      wave.on("play", () => setPlaying(true));
      wave.on("pause", () => setPlaying(false));
      wave.on("finish", () => setPlaying(false));
      player.current = wave;
    });
    return () => {
      cancelled = true;
      player.current?.destroy();
      player.current = null;
    };
  }, [asset.file_url]);

  useEffect(() => {
    // Recoloured in place on a theme change: recreating would stop playback.
    player.current?.setOptions({
      waveColor: cssColor("--wave"),
      progressColor: cssColor("--accent"),
      cursorColor: cssColor("--accent"),
    });
  }, [dark.value]);

  return (
    <div class="audio">
      <div ref={container} />
      <div class="row">
        <button
          class="button small"
          onClick={() => void player.current?.playPause()}
        >
          {playing ? t("preview.pause") : t("preview.play")}
        </button>
      </div>
    </div>
  );
}

function TextPreview({ asset }: { asset: Asset }) {
  const [text, setText] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);
  const [copied, setCopied] = useState<"no" | "yes" | "failed">("no");
  useEffect(() => {
    let cancelled = false;
    setText(null);
    setFailed(false);
    setCopied("no");
    fetch(asset.file_url)
      .then((response) => {
        if (!response.ok) throw new Error(String(response.status));
        return response.text();
      })
      .then((body) => !cancelled && setText(body))
      .catch(() => !cancelled && setFailed(true));
    return () => {
      cancelled = true;
    };
  }, [asset.file_url]);
  function copy(value: string) {
    try {
      // navigator.clipboard is undefined on pages that are not a secure
      // context (the types say otherwise), and the write can be denied.
      void navigator.clipboard.writeText(value).then(
        () => setCopied("yes"),
        () => setCopied("failed"),
      );
    } catch {
      setCopied("failed");
    }
  }
  if (failed) return <p class="hint error">{t("preview.textFailed")}</p>;
  if (text === null) return <span class="spinner" />;
  return (
    <div class="text">
      <pre tabIndex={0}>{text}</pre>
      <div class="row">
        <button class="button small" onClick={() => copy(text)}>
          {copied === "yes"
            ? t("preview.copied")
            : copied === "failed"
              ? t("preview.copyFailed")
              : t("preview.copy")}
        </button>
      </div>
    </div>
  );
}

function VideoPreview({ asset }: { asset: Asset }) {
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [asset.file_url]);
  if (failed) return <p class="hint error">{t("preview.videoUnsupported")}</p>;
  return (
    <video
      src={asset.file_url}
      controls
      loop
      playsInline
      aria-label={assetName(asset)}
      onError={() => setFailed(true)}
    />
  );
}

function ImagePreview({ asset }: { asset: Asset }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <img
        src={asset.file_url}
        alt={assetName(asset)}
        class="zoomable"
        role="button"
        tabIndex={0}
        title={t("preview.openReal")}
        onClick={() => setOpen(true)}
        onKeyDown={(event) => {
          if (event.key === "Enter" || event.key === " ") {
            event.preventDefault();
            setOpen(true);
          }
        }}
      />
      {open && <ImageViewer asset={asset} onClose={() => setOpen(false)} />}
    </>
  );
}

export function AssetPreview({ asset }: { asset: Asset }) {
  if (asset.kind === "image") return <ImagePreview asset={asset} />;
  if (asset.kind === "mesh") return <MeshPreview asset={asset} />;
  if (asset.kind === "video") return <VideoPreview asset={asset} />;
  if (asset.kind === "text") return <TextPreview asset={asset} />;
  return <AudioPreview asset={asset} />;
}
