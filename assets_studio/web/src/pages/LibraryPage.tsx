// All assets: filter by kind or favourites, inspect one, trace what it was made from.
import { useEffect, useState } from "preact/hooks";

import { api, type Asset, type Parent } from "../api/client";
import { AssetActions } from "../components/AssetActions";
import { AssetCard } from "../components/AssetCard";
import { AssetPreview } from "../components/AssetPreview";
import { AssetThumb } from "../components/AssetThumb";
import { assetName, bytes, kindLabel, time } from "../format";
import { go, hrefLibrary } from "../state/router";
import {
  assetById,
  assets,
  attempt,
  jobs,
  putAsset,
  putAssets,
  succeededJobs,
} from "../state/store";

const FILTERS = [
  { id: "all", label: "Все" },
  { id: "image", label: "Картинки" },
  { id: "mesh", label: "3D" },
  { id: "audio", label: "Звук" },
  { id: "video", label: "Видео" },
  { id: "text", label: "Текст" },
  { id: "favorite", label: "★ Избранное" },
] as const;
type Filter = (typeof FILTERS)[number]["id"];
const PAGE = 120;

function Details({ asset }: { asset: Asset }) {
  const [parents, setParents] = useState<Parent[]>([]);
  const [title, setTitle] = useState(asset.title ?? "");
  useEffect(() => {
    setTitle(asset.title ?? "");
    void attempt(() => api.lineage(asset.id)).then((list) =>
      setParents(list ?? []),
    );
  }, [asset.id, asset.title]);
  const producer = [...jobs.value.values()].find((job) =>
    job.outputs.includes(asset.id),
  );

  async function rename() {
    if (title === (asset.title ?? "")) return;
    const updated = await attempt(() => api.updateAsset(asset.id, { title }));
    if (updated) putAsset(updated);
  }

  return (
    <aside class="details" aria-label="Свойства файла">
      <div class="preview">
        <AssetPreview key={asset.id} asset={asset} />
      </div>
      <input
        type="text"
        aria-label="Название"
        value={title}
        placeholder={assetName(asset)}
        onInput={(event) => setTitle(event.currentTarget.value)}
        onBlur={() => void rename()}
        onKeyDown={(event) =>
          event.key === "Enter" && event.currentTarget.blur()
        }
      />
      <div class="actions">
        <AssetActions asset={asset} />
      </div>
      <dl class="meta">
        <dt>Вид</dt>
        <dd>
          {kindLabel(asset.kind)} · {asset.mime}
        </dd>
        <dt>Размер</dt>
        <dd>{bytes(asset.size_bytes)}</dd>
        <dt>Создан</dt>
        <dd>{time(asset.created_at)}</dd>
        {typeof asset.meta.seed === "number" && (
          <>
            <dt>Seed</dt>
            <dd>{asset.meta.seed}</dd>
          </>
        )}
        {producer && (
          <>
            <dt>Задание</dt>
            <dd>{producer.prompt ?? producer.task}</dd>
          </>
        )}
        {producer?.model_snapshot && (
          <>
            <dt>Модель</dt>
            <dd>{String(producer.model_snapshot.name)}</dd>
          </>
        )}
        {asset.source_url && (
          <>
            <dt>Источник</dt>
            <dd>{asset.source_url}</dd>
          </>
        )}
      </dl>
      {parents.length > 0 && (
        <div class="field">
          <span>Сделано из</span>
          <div class="candidates">
            {parents.map((parent) => (
              <a
                key={parent.asset_id}
                class="thumb"
                href={hrefLibrary(parent.asset_id)}
                aria-label="Исходный файл"
              >
                <AssetThumb asset={assetById(parent.asset_id)} />
              </a>
            ))}
          </div>
        </div>
      )}
    </aside>
  );
}

export function LibraryPage({ selectedId }: { selectedId: string | null }) {
  const [filter, setFilter] = useState<Filter>("all");
  const [limit, setLimit] = useState(PAGE);
  const [ids, setIds] = useState<string[] | null>(null);
  const finished = succeededJobs.value;

  useEffect(() => {
    setIds(null);
    setLimit(PAGE);
  }, [filter]);
  useEffect(() => {
    // Ignore a response that arrives after the filter has changed again.
    let current = true;
    const query =
      filter === "favorite"
        ? { favorite: true }
        : filter === "all"
          ? {}
          : { kind: filter };
    void attempt(() => api.assets({ ...query, limit })).then((list) => {
      if (!current) return;
      putAssets(list ?? []);
      setIds(list?.map((asset) => asset.id) ?? []);
    });
    return () => {
      current = false;
    };
    // New results appear as jobs succeed.
  }, [filter, limit, finished]);

  const selected = selectedId ? assetById(selectedId) : undefined;
  return (
    <div class="library">
      <div class="browse">
        <div class="segmented" role="group" aria-label="Показать">
          {FILTERS.map((item) => (
            <button
              key={item.id}
              class={item.id === filter ? "active" : ""}
              aria-pressed={item.id === filter}
              onClick={() => setFilter(item.id)}
            >
              {item.label}
            </button>
          ))}
        </div>
        {ids === null ? (
          <span class="spinner" />
        ) : ids.length === 0 ? (
          <p class="hint">
            Здесь пока пусто. Результаты генераций и загруженные файлы попадают
            сюда.
          </p>
        ) : (
          <>
            <div class="grid">
              {ids.map((id) => {
                const asset = assets.value.get(id);
                // Deleted here a moment ago: gone without reloading the list.
                return asset && !asset.deleted_at ? (
                  <AssetCard
                    key={id}
                    asset={asset}
                    selected={id === selectedId}
                    onClick={() => go(hrefLibrary(id))}
                  />
                ) : null;
              })}
            </div>
            {ids.length === limit && (
              <button class="button" onClick={() => setLimit(limit + PAGE)}>
                Показать ещё
              </button>
            )}
          </>
        )}
      </div>
      {selected && !selected.deleted_at && <Details asset={selected} />}
    </div>
  );
}
