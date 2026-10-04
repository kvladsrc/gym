// Labels for names that come from data, and small formatting helpers.
import type { Asset, InputSpec, Job } from "./api/client";
import { has, language, t } from "./i18n";

/** A task's label; a task the interface does not know shows its name. */
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

/** Asset kind accepted by an input, judged by its MIME patterns. */
export function inputKind(spec: InputSpec): string | null {
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
