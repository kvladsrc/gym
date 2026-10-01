import type { Asset } from "../api/client";
import { assetName, bytes, kindLabel } from "../format";
import { AssetThumb } from "./AssetThumb";

export function AssetCard({
  asset,
  selected = false,
  onClick,
}: {
  asset: Asset;
  selected?: boolean;
  onClick: () => void;
}) {
  return (
    <button
      class={`card${selected ? " selected" : ""}`}
      aria-pressed={selected}
      onClick={onClick}
    >
      <span class="thumb">
        <AssetThumb asset={asset} />
      </span>
      <span class="name" title={assetName(asset)}>
        {asset.favorite ? "★ " : ""}
        {assetName(asset)}
      </span>
      <span class="sub">
        {kindLabel(asset.kind)} · {bytes(asset.size_bytes)}
      </span>
    </button>
  );
}
