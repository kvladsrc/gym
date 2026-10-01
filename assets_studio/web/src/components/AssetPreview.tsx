// Large preview: image, 3D model (model-viewer), audio (waveform + controls),
// video (native player) or text (with copying).
// The 3D and audio libraries are loaded on first use to keep the page light.
import { useEffect, useRef, useState } from "preact/hooks";

import type { Asset } from "../api/client";
import { assetName } from "../format";

let modelViewer: Promise<unknown> | null = null;

function MeshPreview({ asset }: { asset: Asset }) {
  const [ready, setReady] = useState(false);
  useEffect(() => {
    modelViewer ??= import("@google/model-viewer");
    void modelViewer.then(() => setReady(true));
  }, []);
  if (!ready) return <span class="spinner" />;
  return (
    <model-viewer
      src={asset.file_url}
      camera-controls
      auto-rotate
      shadow-intensity="0.6"
      exposure="1.05"
    />
  );
}

function AudioPreview({ asset }: { asset: Asset }) {
  const container = useRef<HTMLDivElement>(null);
  const [playing, setPlaying] = useState(false);
  const player = useRef<{
    playPause: () => Promise<void>;
    destroy: () => void;
  } | null>(null);

  useEffect(() => {
    let cancelled = false;
    void import("wavesurfer.js").then(({ default: WaveSurfer }) => {
      if (cancelled || !container.current) return;
      const wave = WaveSurfer.create({
        container: container.current,
        url: asset.file_url,
        height: 72,
        waveColor: "#b8c2d0",
        progressColor: "#2f6fed",
        cursorColor: "#2f6fed",
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

  return (
    <div class="audio">
      <div ref={container} />
      <div class="row">
        <button
          class="button small"
          onClick={() => void player.current?.playPause()}
        >
          {playing ? "Пауза" : "Слушать"}
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
  if (failed) return <p class="hint error">Не удалось загрузить текст.</p>;
  if (text === null) return <span class="spinner" />;
  return (
    <div class="text">
      <pre tabIndex={0}>{text}</pre>
      <div class="row">
        <button class="button small" onClick={() => copy(text)}>
          {copied === "yes"
            ? "Скопировано"
            : copied === "failed"
              ? "Не удалось скопировать"
              : "Копировать"}
        </button>
      </div>
    </div>
  );
}

function VideoPreview({ asset }: { asset: Asset }) {
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [asset.file_url]);
  if (failed)
    return (
      <p class="hint error">
        Браузер не может воспроизвести это видео — его можно скачать.
      </p>
    );
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

export function AssetPreview({ asset }: { asset: Asset }) {
  if (asset.kind === "image")
    return <img src={asset.file_url} alt={assetName(asset)} />;
  if (asset.kind === "mesh") return <MeshPreview asset={asset} />;
  if (asset.kind === "video") return <VideoPreview asset={asset} />;
  if (asset.kind === "text") return <TextPreview asset={asset} />;
  return <AudioPreview asset={asset} />;
}
