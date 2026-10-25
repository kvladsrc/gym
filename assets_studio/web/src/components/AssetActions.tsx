// Actions on an asset, shared by the section view and the library.
import { useEffect, useState } from "preact/hooks";

import { api, type Asset } from "../api/client";
import { acceptingTasks, sendTo } from "../state/drafts";
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

/** The tasks that take a 3D model and the image it was made from: painting
 * it (3d-paint) and rebuilding its surface finer (3d-refine). */
const WITH_ITS_IMAGE = {
  "3d-paint": {
    one: "asset.paint",
    with: "asset.paintWith",
    title: "asset.paintTitle",
  },
  "3d-refine": {
    one: "asset.refine",
    with: "asset.refineWith",
    title: "asset.refineTitle",
  },
} as const;

type WithItsImage = keyof typeof WITH_ITS_IMAGE;

/** A 3D model and the image it was made from, to paint (Paint 2.1 PBR, ...)
 * or refine (Hi3D): a special case beside the generic "→ section", which
 * would need both inputs picked by hand. One button per server and task,
 * shown when the model's lineage has an image; the job opens in the 3D
 * section. */
export function PaintButtons({ asset }: { asset: Asset }) {
  const [image, setImage] = useState<string | null>(null);
  const actions = servers.value.flatMap((server) =>
    server.tasks
      .map((task) => task.task)
      .filter((task): task is WithItsImage => task in WITH_ITS_IMAGE)
      .map((task) => ({ server, task })),
  );
  const glb = asset.mime === "model/gltf-binary";
  const any = actions.length > 0;
  useEffect(() => {
    setImage(null);
    if (!glb || !any) return;
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
  }, [asset.id, glb, any]);
  if (!image) return null;
  return (
    <>
      {actions.map(({ server, task }) => {
        const labels = WITH_ITS_IMAGE[task];
        const several =
          actions.filter((other) => other.task === task).length > 1;
        return (
          <button
            key={`${server.id}:${task}`}
            class="button small"
            title={t(labels.title)}
            onClick={() =>
              void attempt(() =>
                api.createJob({
                  server: server.id,
                  task,
                  count: 1,
                  inputs: { mesh: asset.id, image },
                }),
              ).then((job) => {
                if (!job) return;
                mergeJob(job);
                go(hrefSection("mesh"));
              })
            }
          >
            {several ? t(labels.with, { server: server.title }) : t(labels.one)}
          </button>
        );
      })}
    </>
  );
}

/** Removing an image's background in one press: image-to-image without a
 * prompt is what a background remover (BiRefNet) declares. One button per
 * such server; the job opens in the image section. */
export function RemoveBackgroundButtons({ asset }: { asset: Asset }) {
  if (asset.kind !== "image") return null;
  const removers = servers.value.filter((candidate) =>
    candidate.tasks.some(
      (task) => task.task === "image-to-image" && task.prompt === "none",
    ),
  );
  return (
    <>
      {removers.map((server) => (
        <button
          key={server.id}
          class="button small"
          title={t("asset.removeBackgroundTitle")}
          onClick={() =>
            void attempt(() =>
              api.createJob({
                server: server.id,
                task: "image-to-image",
                count: 1,
                inputs: { image: asset.id },
              }),
            ).then((job) => {
              if (!job) return;
              mergeJob(job);
              go(hrefSection("image"));
            })
          }
        >
          {removers.length > 1
            ? t("asset.removeBackgroundWith", { server: server.title })
            : t("asset.removeBackground")}
        </button>
      ))}
    </>
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
      <PaintButtons asset={asset} />
      <RemoveBackgroundButtons asset={asset} />
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
