import { useEffect, useState } from "preact/hooks";

import type { Asset } from "../api/client";
import { assetName } from "../format";
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

export function AssetThumb({ asset }: { asset: Asset | undefined }) {
  if (!asset) return <span class="spinner" />;
  if (asset.deleted_at) return <TrashIcon />;
  if (asset.kind === "image")
    return <img src={asset.file_url} alt={assetName(asset)} loading="lazy" />;
  if (asset.kind === "video") return <VideoThumb asset={asset} />;
  if (asset.kind === "mesh") return <CubeIcon />;
  if (asset.kind === "text") return <TextIcon />;
  return <WaveIcon />;
}
