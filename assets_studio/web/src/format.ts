// Labels for names that come from data, and small formatting helpers.
import type { Asset, InputSpec, Job } from "./api/client";
import { has, language, t } from "./i18n";

/** A task's label; a task the interface does not know shows its name. */
// Everything about motion goes to Animation whatever its file format: generating it
// ("…-to-motion", "…-to-native-motion", "…-to-soma-motion"), poses ("image-to-pose"),
// in-betweening, retargeting and observations (2D keypoints) — FBX is shared with rigs,
// native JSON with text.
export function isMotionTask(name: string): boolean {
  return (
    /(^|-)(motion|pose|pose-observations)$/.test(name) ||
    name.startsWith("retarget-")
  );
}

/** Text larger than this is data, not something to read: it is never fetched for a preview. */
const READABLE_TEXT_BYTES = 256 * 1024;

/** The task that made an asset (its "task:…" tag), if any. */
export function assetTask(asset: Asset): string | undefined {
  return asset.tags
    .find((tag) => tag.startsWith("task:"))
    ?.slice("task:".length);
}

/** Motion of any format: motion assets (SMPL-X, SOMA), FBX clips, 2D observations. */
export function isMotionAsset(asset: Asset): boolean {
  if (asset.kind === "motion") return true;
  const task = assetTask(asset);
  return task !== undefined && isMotionTask(task);
}

/** Text a person reads (LLM answers, descriptions); motion and other big JSON are not. */
export function isReadableText(asset: Asset): boolean {
  return (
    asset.kind === "text" &&
    !isMotionAsset(asset) &&
    asset.size_bytes <= READABLE_TEXT_BYTES
  );
}

export const taskLabel = (task: string) => {
  const key = `task.${task}`;
  return has(key) ? t(key) : task;
};

/** What the prompt field holds: a description, or e.g. the lyrics. */
export const promptLabel = (task: string) => {
  const key = `prompt.${task}`;
  return has(key) ? t(key) : t("prompt.description");
};

export const statusLabel = (job: Job) => {
  const key = `status.${job.status}`;
  return has(key) ? t(key) : job.status;
};

export const kindLabel = (kind: string) => {
  const key = `kind.${kind}`;
  return has(key) ? t(key) : kind;
};

/** Body motion and poses, one type per body model (SMPL-X, SOMA). */
export const isMotionMime = (mime: string) =>
  /^application\/vnd\.studio\.[a-z0-9]+-motion\+json$/.test(mime);

/** Asset kind accepted by an input, judged by its MIME patterns. */
export function inputKind(spec: InputSpec): string | null {
  if (spec.mime.length && spec.mime.every(isMotionMime)) return "motion";
  const type = spec.mime[0]?.split("/")[0];
  const kinds: Record<string, string> = {
    image: "image",
    audio: "audio",
    model: "mesh",
    text: "text",
    video: "video",
  };
  return (type && kinds[type]) ?? null;
}

/** File types the studio stores per asset kind (studio/domain.py MIME_KINDS),
 * with extensions for systems that do not map them. A model server declares
 * only its canonical format; the studio converts the rest before sending. */
const STORABLE: Record<string, string> = {
  image: "image/png,image/jpeg,image/webp,.png,.jpg,.jpeg,.webp",
  audio: "audio/wav,audio/flac,audio/ogg,audio/mpeg,.wav,.flac,.ogg,.oga,.mp3",
  mesh: "model/gltf-binary,model/x-fbx,.glb,.fbx",
  video: "video/mp4,video/webm,.mp4,.webm",
  text: "text/plain,.txt",
  motion: "application/json,.json",
};

/** The file chooser filter for an input. */
export function acceptFor(spec: InputSpec): string {
  const kind = inputKind(spec);
  return (kind && STORABLE[kind]) ?? spec.mime.join(",");
}

export function assetName(asset: Asset) {
  return asset.title ?? `${kindLabel(asset.kind)} · ${time(asset.created_at)}`;
}

export function time(iso: string) {
  return new Date(iso).toLocaleString(language.value, {
    day: "2-digit",
    month: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export function duration(seconds: number) {
  return seconds < 60
    ? t("unit.seconds", { s: Math.round(seconds) })
    : t("unit.minutes", {
        m: Math.floor(seconds / 60),
        s: Math.round(seconds % 60),
      });
}

export function bytes(size: number) {
  return size < 1024 * 1024
    ? t("unit.kb", { n: Math.round(size / 1024) })
    : t("unit.mb", {
        n: (size / 1024 / 1024).toLocaleString(language.value, {
          maximumFractionDigits: 1,
        }),
      });
}
