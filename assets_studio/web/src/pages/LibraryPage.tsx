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
import { type Key, t } from "../i18n";
import { Tags } from "../components/Tags";

const FILTERS = [
  { id: "all", label: "library.all" },
  { id: "image", label: "section.image" },
  { id: "mesh", label: "section.mesh" },
  { id: "audio", label: "section.audio" },
  { id: "video", label: "section.video" },
  { id: "text", label: "section.text" },
  { id: "favorite", label: "library.favorite" },
] as const satisfies readonly { id: string; label: Key }[];
type Filter = (typeof FILTERS)[number]["id"];
const PAGE = 120;

/** Rating filter: any, not rated yet, or at least n. */
type RatingFilter = "any" | "unrated" | "1" | "2" | "3" | "4" | "5";

function Details({
  asset,
  onPickTag,
}: {
  asset: Asset;
  onPickTag: (tag: string) => void;
}) {
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

  async function retag(tags: string[]) {
    const updated = await attempt(() => api.updateAsset(asset.id, { tags }));
    if (updated) putAsset(updated);
  }

  async function rename() {
    if (title === (asset.title ?? "")) return;
    const updated = await attempt(() => api.updateAsset(asset.id, { title }));
    if (updated) putAsset(updated);
  }

  return (
    <aside class="details" aria-label={t("library.details")}>
      <div class="preview">
        <AssetPreview key={asset.id} asset={asset} />
      </div>
      <input
        type="text"
        aria-label={t("library.title")}
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
      <Tags
        tags={asset.tags}
        label={t("library.tags")}
        onChange={(tags) => void retag(tags)}
        onPick={onPickTag}
      />
      <dl class="meta">
        <dt>{t("library.kind")}</dt>
        <dd>
          {kindLabel(asset.kind)} · {asset.mime}
        </dd>
        <dt>{t("library.size")}</dt>
        <dd>{bytes(asset.size_bytes)}</dd>
        <dt>{t("library.created")}</dt>
        <dd>{time(asset.created_at)}</dd>
        {typeof asset.meta.seed === "number" && (
          <>
            <dt>Seed</dt>
            <dd>{asset.meta.seed}</dd>
          </>
        )}
        {producer && (
          <>
            <dt>{t("library.job")}</dt>
            <dd>{producer.prompt ?? producer.task}</dd>
          </>
        )}
        {producer?.model_snapshot && (
          <>
            <dt>{t("library.model")}</dt>
            <dd>{String(producer.model_snapshot.name)}</dd>
          </>
        )}
        {asset.source_url && (
          <>
            <dt>{t("library.source")}</dt>
            <dd>{asset.source_url}</dd>
          </>
        )}
      </dl>
      {parents.length > 0 && (
        <div class="field">
          <span>{t("library.madeFrom")}</span>
          <div class="candidates">
            {parents.map((parent) => (
              <a
                key={parent.asset_id}
                class="thumb"
                href={hrefLibrary(parent.asset_id)}
                aria-label={t("library.input")}
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
  const [tagFilter, setTagFilter] = useState<string[]>([]);
  const [rating, setRating] = useState<RatingFilter>("any");
  const [limit, setLimit] = useState(PAGE);
  const [ids, setIds] = useState<string[] | null>(null);
  const finished = succeededJobs.value;

  useEffect(() => {
    setIds(null);
    setLimit(PAGE);
  }, [filter, tagFilter, rating]);
  useEffect(() => {
    // Ignore a response that arrives after the filter has changed again.
    let current = true;
    const query =
      filter === "favorite"
        ? { favorite: true }
        : filter === "all"
          ? {}
          : { kind: filter };
    const scored =
      rating === "any"
        ? {}
        : rating === "unrated"
          ? { unrated: true }
          : { minRating: Number(rating) };
    void attempt(() =>
      api.assets({ ...query, ...scored, tags: tagFilter, limit }),
    ).then((list) => {
      if (!current) return;
      putAssets(list ?? []);
      setIds(list?.map((asset) => asset.id) ?? []);
    });
    return () => {
      current = false;
    };
    // New results appear as jobs succeed.
  }, [filter, tagFilter, rating, limit, finished]);

  const selected = selectedId ? assetById(selectedId) : undefined;
  return (
    <div class="library">
      <div class="browse">
        <div class="segmented" role="group" aria-label={t("library.show")}>
          {FILTERS.map((item) => (
            <button
              key={item.id}
              class={item.id === filter ? "active" : ""}
              aria-pressed={item.id === filter}
              onClick={() => setFilter(item.id)}
            >
              {t(item.label)}
            </button>
          ))}
        </div>
        <div class="filters">
          <Tags
            tags={tagFilter}
            label={t("library.filterTags")}
            onChange={setTagFilter}
          />
          <select
            aria-label={t("asset.rating")}
            value={rating}
            onChange={(event) =>
              setRating(event.currentTarget.value as RatingFilter)
            }
          >
            <option value="any">{t("library.anyRating")}</option>
            <option value="unrated">{t("library.unrated")}</option>
            {["1", "2", "3", "4", "5"].map((n) => (
              <option key={n} value={n}>
                {t("library.minRating", { n })}
              </option>
            ))}
          </select>
        </div>
        {ids === null ? (
          <span class="spinner" />
        ) : ids.length === 0 ? (
          <p class="hint">{t("library.empty")}</p>
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
                {t("library.more")}
              </button>
            )}
          </>
        )}
      </div>
      {selected && !selected.deleted_at && (
        <Details
          asset={selected}
          onPickTag={(tag) =>
            !tagFilter.includes(tag) && setTagFilter([...tagFilter, tag])
          }
        />
      )}
    </div>
  );
}
