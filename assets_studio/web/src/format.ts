// Russian labels and small formatting helpers.
import type { Asset, InputSpec, Job } from "./api/client";

const TASKS: Record<string, string> = {
  "text-to-image": "Текст → картинка",
  "image-to-image": "Картинка → картинка",
  "image-to-3d": "Картинка → 3D",
  "text-to-speech": "Текст → речь",
  "text-to-audio": "Текст → звук",
  "audio-to-audio": "Звук → звук",
  "text-to-text": "Текст → текст",
  "image-to-video": "Картинка → видео",
  "text-to-3d": "Текст → 3D",
};
export const taskLabel = (task: string) => TASKS[task] ?? task;

const STATUSES: Record<string, string> = {
  queued: "В очереди",
  waiting_model: "Ждёт модель",
  running: "Генерация",
  succeeded: "Готово",
  failed: "Ошибка",
  cancelled: "Отменено",
};
export const statusLabel = (job: Job) => STATUSES[job.status] ?? job.status;

const KINDS: Record<string, string> = {
  image: "Картинка",
  mesh: "3D",
  audio: "Звук",
  text: "Текст",
  video: "Видео",
};
export const kindLabel = (kind: string) => KINDS[kind] ?? kind;

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
  mesh: "model/gltf-binary,.glb",
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
  return new Date(iso).toLocaleString("ru-RU", {
    day: "2-digit",
    month: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export function duration(seconds: number) {
  return seconds < 60
    ? `${Math.round(seconds)} с`
    : `${Math.floor(seconds / 60)} мин ${Math.round(seconds % 60)} с`;
}

export function bytes(size: number) {
  return size < 1024 * 1024
    ? `${Math.round(size / 1024)} КБ`
    : `${(size / 1024 / 1024).toFixed(1)} МБ`;
}
