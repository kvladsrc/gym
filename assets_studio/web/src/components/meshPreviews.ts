// Thumbnails of meshes are still pictures (ADR-011). The studio keeps one per
// mesh; the browser draws the missing ones as their thumbnails come into view:
// one model at a time, in a worker, never a live 3D view per card.
import { signal, type Signal } from "@preact/signals";
import type { RefObject } from "preact";
import { useEffect } from "preact/hooks";

import { api, type Asset } from "../api/client";
import { putAsset } from "../state/store";
import { isShapeOnly } from "./clay";
import type { PreviewReply, PreviewRequest } from "./previewRenderer.worker";

/** The renderer's version. Raise it when the pictures change (light, framing,
 * size): older ones are drawn again as their thumbnails are shown. */
export const PREVIEW_VERSION = 1;
/** The longer side, pixels: a card's thumbnail on a high-density screen. */
const SIZE = 512;
/** A model taking longer is given up until the page is reloaded. */
const TIMEOUT_MS = 120_000;
/** The worker, with its GPU memory, ends after this long without work. */
const IDLE_MS = 30_000;
const GLB = "model/gltf-binary";

type Drawing =
  { state: "drawing" } | { state: "failed" } | { state: "done"; url: string };

/** What this page has drawn, by asset; each asset is drawn at most once. A
 * signal per asset: a new picture re-renders only its own thumbnails. */
const drawings = new Map<string, Signal<Drawing | undefined>>();
function drawingOf(id: string) {
  let drawing = drawings.get(id);
  if (!drawing) {
    drawing = signal<Drawing | undefined>(undefined);
    drawings.set(id, drawing);
  }
  return drawing;
}

/** False once this browser has shown it cannot draw at all. */
const supported = signal(
  typeof Worker !== "undefined" && typeof OffscreenCanvas !== "undefined",
);

/** Models of thumbnails in view that need a picture, in the order they came
 * into view, with how many thumbnails show each. */
const wanted = new Map<string, { asset: Asset; shown: number }>();
let busy = false;

let worker: Worker | null = null;
/** The worker has answered once: a later failure is a model's, not the browser's. */
let answered = false;
let idleTimer: number | undefined;
let settle: ((reply: PreviewReply) => void) | null = null;

function startWorker() {
  const started = new Worker(
    new URL("./previewRenderer.worker.ts", import.meta.url),
    { type: "module" },
  );
  started.addEventListener("message", (event: MessageEvent<PreviewReply>) => {
    answered = true;
    settle?.(event.data);
  });
  // It did not load, or failed outside a drawing.
  started.addEventListener("error", (event) => {
    event.preventDefault();
    stopWorker();
    settle?.({
      id: "",
      error: event.message || "the renderer stopped",
      fatal: !answered,
    });
  });
  return started;
}

function stopWorker() {
  worker?.terminate();
  worker = null;
}

function draw(asset: Asset): Promise<Blob> {
  window.clearTimeout(idleTimer);
  worker ??= startWorker();
  const running = worker;
  const request: PreviewRequest = {
    id: asset.id,
    url: asset.file_url,
    clay: isShapeOnly(asset),
    size: SIZE,
  };
  return new Promise((resolve, reject) => {
    const timer = window.setTimeout(() => {
      stopWorker(); // stuck on this model, perhaps: a new worker draws the next
      settle?.({ id: request.id, error: "timed out", fatal: false });
    }, TIMEOUT_MS);
    settle = (reply) => {
      if (reply.id !== request.id && reply.id !== "") return;
      window.clearTimeout(timer);
      settle = null;
      if ("picture" in reply) {
        resolve(reply.picture);
        return;
      }
      if (reply.fatal) {
        supported.value = false;
        stopWorker();
      }
      reject(new Error(reply.error));
    };
    running.postMessage(request);
  });
}

async function pump() {
  if (busy) return;
  const next = wanted.values().next().value;
  if (!next || !supported.value) {
    window.clearTimeout(idleTimer);
    idleTimer = window.setTimeout(stopWorker, IDLE_MS);
    return;
  }
  busy = true;
  const { asset } = next;
  wanted.delete(asset.id);
  const drawing = drawingOf(asset.id);
  drawing.value = { state: "drawing" };
  try {
    const picture = await draw(asset);
    const updated = await api.putPreview(asset.id, picture, PREVIEW_VERSION);
    drawing.value = updated.preview_url
      ? { state: "done", url: updated.preview_url }
      : { state: "failed" };
    putAsset(updated);
  } catch (error) {
    drawing.value = { state: "failed" };
    console.warn(
      `No preview of ${asset.id}: ${error instanceof Error ? error.message : String(error)}`,
    );
  } finally {
    busy = false;
  }
  void pump();
}

function want(asset: Asset) {
  const entry = wanted.get(asset.id);
  if (entry) entry.shown += 1;
  else wanted.set(asset.id, { asset, shown: 1 });
  void pump();
}

function unwant(id: string) {
  const entry = wanted.get(id);
  if (!entry) return;
  entry.shown -= 1;
  if (entry.shown === 0) wanted.delete(id);
}

const watchers = new Map<Element, (visible: boolean) => void>();
let observer: IntersectionObserver | null = null;

/** Calls `onChange` as the element comes into view and leaves it; one
 * observer serves every thumbnail. Returns the function that stops it. */
function watch(element: Element, onChange: (visible: boolean) => void) {
  observer ??= new IntersectionObserver(
    (entries) => {
      for (const entry of entries)
        watchers.get(entry.target)?.(entry.isIntersecting);
    },
    { rootMargin: "100px" },
  );
  watchers.set(element, onChange);
  observer.observe(element);
  return () => {
    watchers.delete(element);
    observer?.unobserve(element);
  };
}

export interface MeshPreview {
  /** The picture to show, if any: perhaps an outdated one while a new one is drawn. */
  url: string | null;
  drawing: boolean;
}

/** A mesh's thumbnail picture. While `element` is in view and the stored
 * picture is missing, outdated or `broken` (that URL did not load), this page
 * draws a new one and keeps it in the studio. */
export function useMeshPreview(
  asset: Asset,
  element: RefObject<Element>,
  broken: string | null,
): MeshPreview {
  const drawn = drawingOf(asset.id).value;
  const stored = asset.preview_url === broken ? null : asset.preview_url;
  const current =
    stored !== null && (asset.preview_version ?? 0) >= PREVIEW_VERSION;
  const needed =
    supported.value &&
    !current &&
    drawn === undefined &&
    asset.mime === GLB &&
    !asset.deleted_at;
  useEffect(() => {
    const node = element.current;
    if (!needed || !node) return;
    let visible = false;
    const unwatch = watch(node, (now) => {
      if (now === visible) return;
      visible = now;
      if (now) want(asset);
      else unwant(asset.id);
    });
    return () => {
      unwatch();
      if (visible) unwant(asset.id);
    };
    // Edits make a new asset object; its model stays the same.
  }, [asset.id, needed]);
  const fresh =
    drawn?.state === "done" && drawn.url !== broken ? drawn.url : null;
  return {
    url: current ? stored : (fresh ?? stored),
    drawing: drawn?.state === "drawing",
  };
}
