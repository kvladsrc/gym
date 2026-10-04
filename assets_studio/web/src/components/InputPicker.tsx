// One input of a task: drop or choose a file, paste a URL, or pick from the library.
import { useRef, useState } from "preact/hooks";

import { api, type InputSpec } from "../api/client";
import { acceptFor, assetName, inputKind, kindLabel } from "../format";
import { assetById, attempt, putAsset } from "../state/store";
import { AssetThumb } from "./AssetThumb";
import { LibraryPicker } from "./LibraryPicker";
import { localized, t } from "../i18n";

export function InputPicker({
  spec,
  assetId,
  onChange,
}: {
  spec: InputSpec;
  assetId: string | null;
  onChange: (assetId: string | null) => void;
}) {
  const [over, setOver] = useState(false);
  const [url, setUrl] = useState("");
  const [picking, setPicking] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);
  const kind = inputKind(spec);
  const asset = assetId ? assetById(assetId) : undefined;

  async function upload(file: File) {
    const created = await attempt(() =>
      api.upload(file, file.name.replace(/\.[^.]+$/, "")),
    );
    if (created) {
      putAsset(created);
      onChange(created.id);
    }
  }

  async function fromUrl() {
    const created = await attempt(() => api.importUrl(url.trim()));
    if (created) {
      putAsset(created);
      onChange(created.id);
      setUrl("");
    }
  }

  return (
    <div class="field">
      <span>
        {localized(
          spec.labels,
          spec.description ?? (kind ? kindLabel(kind) : spec.role),
        )}
        {spec.required ? "" : t("optional")}
      </span>
      <div
        class={`dropzone${over ? " over" : ""}`}
        onDragOver={(event) => {
          event.preventDefault();
          setOver(true);
        }}
        onDragLeave={() => setOver(false)}
        onDrop={(event) => {
          event.preventDefault();
          setOver(false);
          const file = event.dataTransfer?.files[0];
          if (file) void upload(file);
        }}
      >
        {assetId ? (
          <>
            <span class="thumb">
              <AssetThumb asset={asset} />
            </span>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div class="card-name">{asset ? assetName(asset) : "…"}</div>
              <button class="button small" onClick={() => onChange(null)}>
                {t("input.remove")}
              </button>
            </div>
          </>
        ) : (
          <span class="empty">{t("input.drop")}</span>
        )}
      </div>
      <div class="row">
        <button class="button small" onClick={() => fileInput.current?.click()}>
          {t("input.file")}
        </button>
        <input
          ref={fileInput}
          type="file"
          hidden
          accept={acceptFor(spec)}
          onChange={(event) => {
            const file = event.currentTarget.files?.[0];
            if (file) void upload(file);
            event.currentTarget.value = "";
          }}
        />
        {kind && (
          <button class="button small" onClick={() => setPicking(true)}>
            {t("input.library")}
          </button>
        )}
      </div>
      <div class="row">
        <input
          type="url"
          aria-label={t("input.url")}
          placeholder={t("input.urlPlaceholder")}
          value={url}
          onInput={(event) => setUrl(event.currentTarget.value)}
        />
        <button
          class="button small"
          disabled={!url.trim()}
          onClick={() => void fromUrl()}
        >
          {t("input.upload")}
        </button>
      </div>
      {picking && kind && (
        <LibraryPicker
          kind={kind}
          onClose={() => setPicking(false)}
          onPick={(picked) => {
            onChange(picked.id);
            setPicking(false);
          }}
        />
      )}
    </div>
  );
}
