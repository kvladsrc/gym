// Actions on an asset, shared by the section view and the library.
import { useEffect, useState } from "preact/hooks";

import { api, type Asset } from "../api/client";
import { acceptingTasks, sendTo } from "../state/drafts";
import { sections } from "../state/sections";
import { attempt, putAsset, say } from "../state/store";

export function FavoriteButton({ asset }: { asset: Asset }) {
  const label = asset.favorite ? "Убрать из избранного" : "В избранное";
  return (
    <button
      class={`button small star${asset.favorite ? " on" : ""}`}
      title={label}
      aria-label={label}
      aria-pressed={asset.favorite}
      onClick={() =>
        void attempt(() =>
          api.updateAsset(asset.id, { favorite: !asset.favorite }),
        ).then((updated) => updated && putAsset(updated))
      }
    >
      ★
    </button>
  );
}

/** Deleting asks once more on the same button: no dialog to dismiss. */
export function DeleteButton({ asset }: { asset: Asset }) {
  const [asking, setAsking] = useState(false);
  useEffect(() => setAsking(false), [asset.id]);
  if (!asking)
    return (
      <button class="button small" onClick={() => setAsking(true)}>
        Удалить
      </button>
    );
  return (
    <button
      class="button small danger"
      onBlur={() => setAsking(false)}
      onClick={() =>
        void attempt(() => api.deleteAsset(asset.id)).then((deleted) => {
          if (!deleted) return;
          putAsset(deleted);
          say("Удалено из библиотеки");
        })
      }
    >
      Точно удалить?
    </button>
  );
}

/** Download, favourite, "use as input" in each section that accepts the
 * asset, and delete. */
export function AssetActions({ asset }: { asset: Asset }) {
  const targets = sections.value.filter(
    (section) => acceptingTasks(section, asset).length > 0,
  );
  return (
    <>
      <a class="button small" href={asset.file_url} download>
        Скачать
      </a>
      <FavoriteButton asset={asset} />
      {targets.map((section) => (
        <button
          key={section.id}
          class="button small"
          title={`Использовать как вход в разделе «${section.title}»`}
          onClick={() => sendTo(section, asset)}
        >
          → {section.title}
        </button>
      ))}
      <DeleteButton asset={asset} />
    </>
  );
}
