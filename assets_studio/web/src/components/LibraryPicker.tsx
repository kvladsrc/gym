// Dialog with library assets of one kind; picking one returns it.
import { useCallback, useEffect, useRef, useState } from "preact/hooks";

import { api, type Asset } from "../api/client";
import { kindLabel } from "../format";
import { attempt, putAssets } from "../state/store";
import { AssetCard } from "./AssetCard";

export function LibraryPicker({
  kind,
  onPick,
  onClose,
}: {
  kind: string;
  onPick: (asset: Asset) => void;
  onClose: () => void;
}) {
  const [items, setItems] = useState<Asset[] | null>(null);

  useEffect(() => {
    void attempt(() => api.assets({ kind })).then((list) => {
      putAssets(list ?? []);
      setItems(list ?? []);
    });
  }, [kind]);

  // Escape while focus is outside the dialog (effects run after the first paint;
  // the handlers on the elements below work from the first moment).
  const close = useRef(onClose);
  close.current = onClose;
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") close.current();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);
  // A stable ref callback: focus the dialog once, not on every re-render.
  const focusOnce = useCallback(
    (element: HTMLDivElement | null) => element?.focus(),
    [],
  );

  const title = `Библиотека · ${kindLabel(kind)}`;
  return (
    <div
      class="overlay"
      onClick={onClose}
      onKeyDown={(event) => {
        if (event.key === "Escape") {
          event.stopPropagation();
          onClose();
        }
      }}
    >
      <div
        class="modal"
        role="dialog"
        aria-modal="true"
        aria-label={title}
        tabIndex={-1}
        ref={focusOnce}
        onClick={(event) => event.stopPropagation()}
      >
        <div class="row">
          <h3 style={{ flex: 1 }}>{title}</h3>
          <button class="button small" onClick={onClose}>
            Закрыть
          </button>
        </div>
        {items === null ? (
          <span class="spinner" />
        ) : items.length === 0 ? (
          <p class="hint">В библиотеке пока нет файлов этого вида.</p>
        ) : (
          <div class="grid">
            {items.map((asset) => (
              <AssetCard
                key={asset.id}
                asset={asset}
                onClick={() => onPick(asset)}
              />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
