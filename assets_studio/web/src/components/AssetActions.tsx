// Actions on an asset, shared by the section view and the library.
import { useEffect, useState } from "preact/hooks";

import { api, type Asset } from "../api/client";
import { acceptingTasks, sendTo, showJob } from "../state/drafts";
import { go, hrefSection } from "../state/router";
import { sections } from "../state/sections";
import {
  assetById,
  attempt,
  mergeJob,
  putAsset,
  say,
  servers,
} from "../state/store";
import { t } from "../i18n";

export function FavoriteButton({ asset }: { asset: Asset }) {
  const label = asset.favorite ? t("asset.unfavorite") : t("asset.favorite");
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

/** A rating 0-5 (ADR-007); pressing the current one clears it. */
export function RatingButtons({ asset }: { asset: Asset }) {
  const rate = (value: number) =>
    void attempt(() =>
      api.updateAsset(asset.id, {
        rating: asset.rating === value ? null : value,
      }),
    ).then((updated) => updated && putAsset(updated));
  return (
    <span class="segmented rating" role="group" aria-label={t("asset.rating")}>
      {[0, 1, 2, 3, 4, 5].map((value) => (
        <button
          key={value}
          class={asset.rating === value ? "active" : ""}
          aria-pressed={asset.rating === value}
          title={t("asset.rate", { n: value })}
          aria-label={t("asset.rate", { n: value })}
          onClick={() => rate(value)}
        >
          {value}
        </button>
      ))}
    </span>
  );
}

/** Painting a 3D model with the image it was made from (3d-paint): a
 * special case beside the generic "→ section", which would need both inputs
 * picked by hand. Shown when a server paints and the model's lineage has an
 * image; the job opens in the 3D section. */
export function PaintButton({ asset }: { asset: Asset }) {
  const [image, setImage] = useState<string | null>(null);
  const server = servers.value.find((candidate) =>
    candidate.tasks.some((task) => task.task === "3d-paint"),
  );
  const glb = asset.mime === "model/gltf-binary";
  useEffect(() => {
    setImage(null);
    if (!glb || !server) return;
    let current = true;
    void attempt(() => api.lineage(asset.id)).then(async (parents) => {
      for (const parent of parents ?? []) {
        const source =
          assetById(parent.asset_id) ??
          (await attempt(() => api.asset(parent.asset_id)));
        if (source?.kind === "image" && !source.deleted_at) {
          if (current) setImage(source.id);
          return;
        }
      }
    });
    return () => {
      current = false;
    };
  }, [asset.id, glb, server?.id]);
  if (!server || !image) return null;
  return (
    <button
      class="button small"
      title={t("asset.paintTitle")}
      onClick={() =>
        void attempt(() =>
          api.createJob({
            server: server.id,
            task: "3d-paint",
            count: 1,
            inputs: { mesh: asset.id, image },
          }),
        ).then((job) => {
          if (!job) return;
          mergeJob(job);
          showJob("mesh", job.id);
          go(hrefSection("mesh"));
        })
      }
    >
      {t("asset.paint")}
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
        {t("asset.delete")}
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
          say(t("asset.deleted"));
        })
      }
    >
      {t("asset.deleteConfirm")}
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
        {t("asset.download")}
      </a>
      <FavoriteButton asset={asset} />
      <RatingButtons asset={asset} />
      <PaintButton asset={asset} />
      {targets.map((section) => (
        <button
          key={section.id}
          class="button small"
          title={t("asset.sendTitle", { section: section.title })}
          onClick={() => sendTo(section, asset)}
        >
          → {section.title}
        </button>
      ))}
      <DeleteButton asset={asset} />
    </>
  );
}
