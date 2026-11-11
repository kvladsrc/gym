import { useEffect, useRef, useState } from "preact/hooks";

import type { Asset } from "../api/client";
import { assetName, isMotionAsset, isReadableText } from "../format";
import {
  CubeIcon,
  DataIcon,
  FilmIcon,
  MotionIcon,
  TextIcon,
  TrashIcon,
  WaveIcon,
} from "./Icons";
import { useMeshPreview } from "./meshPreviews";

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

/** A still picture of the model, front-on, drawn by this browser the first
 * time it is shown (ADR-011): a live 3D view per thumbnail would load every
 * model in the list at once. FBX (rigs, ADR-006) cannot be drawn: a cube. */
function MeshThumb({ asset }: { asset: Asset }) {
  const box = useRef<HTMLSpanElement>(null);
  const [broken, setBroken] = useState<string | null>(null);
  const { url, drawing } = useMeshPreview(asset, box, broken);
  return (
    <span class="mesh-thumb" ref={box}>
      {url ? (
        <img
          src={url}
          alt={assetName(asset)}
          loading="lazy"
          onError={() => setBroken(url)}
        />
      ) : drawing ? (
        <span class="spinner" />
      ) : (
        <CubeIcon />
      )}
    </span>
  );
}

export function AssetThumb({ asset }: { asset: Asset | undefined }) {
  if (!asset) return <span class="spinner" />;
  if (asset.deleted_at) return <TrashIcon />;
  if (asset.kind === "image")
    return <img src={asset.file_url} alt={assetName(asset)} loading="lazy" />;
  if (asset.kind === "video") return <VideoThumb asset={asset} />;
  if (isMotionAsset(asset)) return <MotionIcon />;
  if (asset.kind === "mesh") return <MeshThumb asset={asset} />;
  if (asset.kind === "text")
    return isReadableText(asset) ? <TextIcon /> : <DataIcon />;
  return <WaveIcon />;
}
