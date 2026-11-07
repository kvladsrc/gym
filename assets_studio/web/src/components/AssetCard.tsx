import type { Asset } from "../api/client";
import type { TargetedMouseEvent } from "preact";
import { assetName, bytes, kindLabel } from "../format";
import { t } from "../i18n";
import { AssetThumb } from "./AssetThumb";

export function AssetCard({
  asset,
  selected = false,
  onClick,
  selection,
}: {
  asset: Asset;
  selected?: boolean;
  onClick: (event: TargetedMouseEvent<HTMLButtonElement>) => void;
  selection?: {
    checked: boolean;
    disabled: boolean;
    onClick: (event: TargetedMouseEvent<HTMLInputElement>) => void;
  };
}) {
  const card = (
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
  if (!selection) return card;
  return (
    <div class="selectable-card">
      {card}
      <input
        class="asset-select"
        type="checkbox"
        aria-label={t("library.selectAsset", { name: assetName(asset) })}
        checked={selection.checked}
        disabled={selection.disabled}
        onClick={selection.onClick}
      />
    </div>
  );
}
