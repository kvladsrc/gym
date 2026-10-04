import { useEffect, useState } from "preact/hooks";

import type { Asset } from "../api/client";
import { assetName } from "../format";
import { clayIfShapeOnly, useModelViewer } from "./AssetPreview";
import { CubeIcon, FilmIcon, TextIcon, TrashIcon, WaveIcon } from "./Icons";

function VideoThumb({ asset }: { asset: Asset }) {
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [asset.file_url]); // the picker reuses this
  if (failed) return <FilmIcon />;
  // The first frame; "#t=0.1" makes browsers draw it without playing.
  return (
    <video
      src={`${asset.file_url}#t=0.1`}
      preload="metadata"
      muted
      playsInline
      aria-label={assetName(asset)}
      onError={() => setFailed(true)}
    />
  );
}

/** The model itself, still and front-on. All <model-viewer> elements share
 * one WebGL context; lazy ones load when they scroll into view. FBX (rigs,
 * ADR-006) cannot be shown: a cube. */
function MeshThumb({ asset }: { asset: Asset }) {
  const glb = asset.mime === "model/gltf-binary";
  const ready = useModelViewer(glb);
  if (!glb || !ready) return <CubeIcon />;
  return (
    <model-viewer
      src={asset.file_url}
      loading="lazy"
      interaction-prompt="none"
      shadow-intensity="0"
      aria-label={assetName(asset)}
      onLoad={clayIfShapeOnly(asset)}
    />
  );
}

export function AssetThumb({ asset }: { asset: Asset | undefined }) {
  if (!asset) return <span class="spinner" />;
  if (asset.deleted_at) return <TrashIcon />;
  if (asset.kind === "image")
    return <img src={asset.file_url} alt={assetName(asset)} loading="lazy" />;
  if (asset.kind === "video") return <VideoThumb asset={asset} />;
  if (asset.kind === "mesh") return <MeshThumb asset={asset} />;
  if (asset.kind === "text") return <TextIcon />;
  return <WaveIcon />;
}
