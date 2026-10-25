// All assets: filter by kind or favourites, inspect one, trace what it was made from.
import { useEffect, useRef, useState } from "preact/hooks";

import { api, type Asset, type Parent } from "../api/client";
import { AssetActions } from "../components/AssetActions";
import { AssetCard } from "../components/AssetCard";
import { AssetPreview } from "../components/AssetPreview";
import { AssetThumb } from "../components/AssetThumb";
import { Provenance } from "../components/Provenance";
import { assetName, bytes, kindLabel, time } from "../format";
import { go, hrefLibrary } from "../state/router";
import {
  assetById,
  assets,
  attempt,
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
  { id: "animation", label: "section.animation" },
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
  preview = true,
  onPickTag,
}: {
  asset: Asset;
  /** False when the viewer already shows the asset large. */
  preview?: boolean;
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
      {preview && (
        <div class="preview">
          <AssetPreview key={asset.id} asset={asset} />
        </div>
      )}
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
        {asset.source_url && (
          <>
            <dt>{t("library.source")}</dt>
            <dd>{asset.source_url}</dd>
          </>
        )}
      </dl>
      <Provenance asset={asset} />
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

/** Grid: many at once. Viewer: one large, as in a section, with the list
 * as a strip below and ←/→ to step through it. Remembered in this browser. */
type View = "grid" | "viewer";
const VIEW_KEY = "assets-studio.library-view";

function rememberedView(): View {
  try {
    return localStorage.getItem(VIEW_KEY) === "viewer" ? "viewer" : "grid";
  } catch {
    return "grid"; // storage blocked
  }
}

function remember(view: View) {
  try {
    localStorage.setItem(VIEW_KEY, view);
  } catch {
    // storage blocked: the choice lasts until the page is reloaded
  }
}

/** Arrow keys belong to the field being typed in, not to the viewer. */
function typing(target: EventTarget | null) {
  return (
    target instanceof HTMLElement &&
    (target.isContentEditable ||
      ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName))
  );
}

export function LibraryPage({ selectedId }: { selectedId: string | null }) {
  const [filter, setFilter] = useState<Filter>("all");
  const [tagFilter, setTagFilter] = useState<string[]>([]);
  const [rating, setRating] = useState<RatingFilter>("any");
  const [limit, setLimit] = useState(PAGE);
  const [ids, setIds] = useState<string[] | null>(null);
  const [view, setView] = useState<View>(rememberedView);
  const strip = useRef<HTMLDivElement>(null);
  const finished = succeededJobs.value;

  // A new filter in the viewer: show the first of the new list once it loads.
  const jump = useRef(false);
  useEffect(() => {
    setIds(null);
    setLimit(PAGE);
  }, [filter, tagFilter, rating]);
  function refilter<T>(set: (value: T) => void) {
    return (value: T) => {
      jump.current = true;
      set(value);
    };
  }
  useEffect(() => {
    // Ignore a response that arrives after the filter has changed again.
    let current = true;
    const query =
      filter === "favorite"
        ? { favorite: true }
        : filter === "all"
          ? {}
          : filter === "animation"
            ? { motion: true } // native JSON and FBX clips alike
            : filter === "mesh" || filter === "text"
              ? { kind: filter, motion: false } // their animations are under Animation
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

  // Deleted here a moment ago: gone without reloading the list.
  const listed = (ids ?? []).flatMap((id) => {
    const asset = assets.value.get(id);
    return asset && !asset.deleted_at ? [asset] : [];
  });
  const shown = listed.map((asset) => asset.id);
  const selected = selectedId ? assetById(selectedId) : undefined;
  const position = selectedId ? shown.indexOf(selectedId) : -1;
  const viewer = view === "viewer";

  // Where the viewer is, ahead of rendering: quick key presses add up.
  const at = useRef<string | null | undefined>(selectedId);
  at.current = selectedId;
  function step(by: number) {
    const next = (at.current ? shown.indexOf(at.current) : -1) + by;
    if (next >= 0 && next < shown.length) {
      at.current = shown[next];
      go(hrefLibrary(shown[next]));
    }
    // At the end of what is loaded: load more; the next press goes on.
    if (next >= shown.length && ids?.length === limit) setLimit(limit + PAGE);
  }

  // The viewer always shows something: the first one when none is picked or
  // the filter has just changed (a link to one asset keeps it, listed or not).
  useEffect(() => {
    if (!viewer || ids === null || !shown.length) return;
    if (!selectedId || (jump.current && !shown.includes(selectedId)))
      go(hrefLibrary(shown[0]));
    jump.current = false;
  }, [viewer, selectedId, ids]);
  useEffect(() => {
    if (!viewer) return;
    function onKey(event: KeyboardEvent) {
      if (typing(event.target) || event.altKey || event.ctrlKey) return;
      if (event.key === "ArrowLeft") step(-1);
      else if (event.key === "ArrowRight") step(1);
      else return;
      event.preventDefault();
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  });
  useEffect(() => {
    strip.current
      ?.querySelector(".thumb.selected")
      ?.scrollIntoView({ block: "nearest", inline: "nearest" });
  }, [selectedId, viewer]);

  function choose(next: View) {
    setView(next);
    remember(next);
  }

  const more = ids?.length === limit && (
    <button class="button" onClick={() => setLimit(limit + PAGE)}>
      {t("library.more")}
    </button>
  );

  return (
    <div class={`library${viewer ? " viewing" : ""}`}>
      <div class="browse">
        <div class="library-top">
          <div class="segmented" role="group" aria-label={t("library.show")}>
            {FILTERS.map((item) => (
              <button
                key={item.id}
                class={item.id === filter ? "active" : ""}
                aria-pressed={item.id === filter}
                onClick={() => refilter(setFilter)(item.id)}
              >
                {t(item.label)}
              </button>
            ))}
          </div>
          <div class="segmented" role="group" aria-label={t("library.view")}>
            {(["grid", "viewer"] as const).map((item) => (
              <button
                key={item}
                class={item === view ? "active" : ""}
                aria-pressed={item === view}
                onClick={() => choose(item)}
              >
                {t(item === "grid" ? "library.viewGrid" : "library.viewLarge")}
              </button>
            ))}
          </div>
        </div>
        <div class="filters">
          <Tags
            tags={tagFilter}
            label={t("library.filterTags")}
            onChange={refilter(setTagFilter)}
          />
          <select
            aria-label={t("asset.rating")}
            value={rating}
            onChange={(event) =>
              refilter(setRating)(event.currentTarget.value as RatingFilter)
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
        ) : shown.length === 0 ? (
          <p class="hint">{t("library.empty")}</p>
        ) : viewer ? (
          <>
            <div class="viewer-head">
              <button
                class="button small"
                aria-label={t("library.previous")}
                disabled={position <= 0}
                onClick={() => step(-1)}
              >
                ←
              </button>
              <span class="viewer-name" title={selected && assetName(selected)}>
                {selected ? assetName(selected) : ""}
              </span>
              <span class="hint">
                {position >= 0 &&
                  t("library.position", { n: position + 1, of: shown.length })}
              </span>
              <button
                class="button small"
                aria-label={t("library.next")}
                disabled={position < 0 || position >= shown.length - 1}
                onClick={() => step(1)}
              >
                →
              </button>
            </div>
            <div class="preview">
              {selected && !selected.deleted_at ? (
                <AssetPreview key={selected.id} asset={selected} />
              ) : (
                <span class="spinner" />
              )}
            </div>
            <div class="history" ref={strip}>
              {listed.map((asset) => (
                <button
                  key={asset.id}
                  class={`thumb${asset.id === selectedId ? " selected" : ""}`}
                  aria-pressed={asset.id === selectedId}
                  title={assetName(asset)}
                  onClick={() => go(hrefLibrary(asset.id))}
                >
                  <AssetThumb asset={asset} />
                </button>
              ))}
              {more}
            </div>
          </>
        ) : (
          <>
            <div class="grid">
              {listed.map((asset) => (
                <AssetCard
                  key={asset.id}
                  asset={asset}
                  selected={asset.id === selectedId}
                  onClick={() => go(hrefLibrary(asset.id))}
                />
              ))}
            </div>
            {more}
          </>
        )}
      </div>
      {selected && !selected.deleted_at && (
        <Details
          asset={selected}
          preview={!viewer}
          onPickTag={(tag) =>
            !tagFilter.includes(tag) && setTagFilter([...tagFilter, tag])
          }
        />
      )}
    </div>
  );
}
