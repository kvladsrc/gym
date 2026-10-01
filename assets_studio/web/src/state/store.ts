// Global state: model servers and jobs kept current by the server-sent event
// stream, and a cache of assets loaded on demand.
import { computed, signal } from "@preact/signals";

import { api, type Asset, type Job, type Server } from "../api/client";

export const servers = signal<Server[]>([]);
export const jobs = signal<ReadonlyMap<string, Job>>(new Map());
export const assets = signal<ReadonlyMap<string, Asset>>(new Map());
export const connected = signal(false);
/** True once the first state has arrived (avoids flashing an empty page). */
export const loaded = signal(false);
export const notice = signal<{ text: string; error: boolean } | null>(null);

const PENDING = new Set(["queued", "waiting_model", "running"]);
export const isPending = (job: Job) => PENDING.has(job.status);
export const activeJobs = computed(
  () => [...jobs.value.values()].filter(isPending).length,
);
/** Changes whenever a job succeeds: new assets may have appeared. */
export const succeededJobs = computed(
  () =>
    [...jobs.value.values()].filter((job) => job.status === "succeeded").length,
);

/** Merge job states from any source: events, API responses, a resync.
 *
 * States of one job can arrive out of order (a fast failure event can overtake
 * the response that created the job); the higher version always wins. */
export function mergeJobs(incoming: Iterable<Job>) {
  let next: Map<string, Job> | null = null;
  for (const job of incoming) {
    const current = (next ?? jobs.value).get(job.id);
    if (current && current.version > job.version) continue;
    next ??= new Map(jobs.value);
    next.set(job.id, job);
  }
  if (next) jobs.value = next;
}

export const mergeJob = (job: Job) => mergeJobs([job]);

export function putAssets(incoming: Iterable<Asset>) {
  const next = new Map(assets.value);
  for (const asset of incoming) next.set(asset.id, asset);
  assets.value = next;
}

export const putAsset = (asset: Asset) => putAssets([asset]);

const loading = new Set<string>();

/** The asset if cached; otherwise starts loading it and returns undefined. */
export function assetById(id: string): Asset | undefined {
  if (!id) return undefined;
  const cached = assets.value.get(id);
  if (!cached && !loading.has(id)) {
    loading.add(id);
    api
      .asset(id)
      .then(putAsset)
      .catch(() => undefined)
      .finally(() => loading.delete(id));
  }
  return cached;
}

let noticeTimer: number | undefined;

export function say(text: string, error = false) {
  notice.value = { text, error };
  window.clearTimeout(noticeTimer);
  noticeTimer = window.setTimeout(
    () => (notice.value = null),
    error ? 8000 : 3000,
  );
}

/** Run an API call; show its error as a notice instead of throwing. */
export async function attempt<T>(
  action: () => Promise<T>,
): Promise<T | undefined> {
  try {
    return await action();
  } catch (error) {
    say(error instanceof Error ? error.message : String(error), true);
    return undefined;
  }
}

async function resync() {
  const [serverList, jobList] = await Promise.all([api.servers(), api.jobs()]);
  servers.value = serverList;
  // Merge, not replace: an event newer than this snapshot may already be here.
  mergeJobs(jobList);
  loaded.value = true;
}

/** Load the initial state and follow changes. EventSource reconnects by itself. */
export function connect() {
  const events = new EventSource("/api/events");
  events.onopen = () => {
    connected.value = true;
    void attempt(resync);
  };
  events.onerror = () => (connected.value = false);
  events.addEventListener("servers", (event) => {
    servers.value = JSON.parse(
      (event as MessageEvent<string>).data,
    ) as Server[];
  });
  events.addEventListener("job", (event) => {
    mergeJob(JSON.parse((event as MessageEvent<string>).data) as Job);
  });
}
