// A model as a coloured chip, so the jobs of one model are found at a glance: each model keeps
// one hue wherever it appears (queue, history, a job's details). The hue follows the model's
// place in the server list, so models listed next to each other (one section's, often one
// chain's) get hues far apart. There are more models than clearly different hues, so the
// models of the jobs at hand are told apart, the queue's first: one whose hue another already
// has takes the next free one. The queue's hues change only when the queue's models do.
import { computed } from "@preact/signals";
import type { CSSProperties } from "preact";

import { serverById } from "../state/sections";
import { isPending, jobs, servers } from "../state/store";

/** OKLCH hues, ordered so that neighbours in this list are far apart on the colour wheel. */
const HUES = [25, 180, 320, 125, 265, 55, 205, 350, 150, 290, 85, 235];

/** The model's place in the server list; one the studio does not list goes by its name. */
function order(server: string): number {
  const index = servers.value.findIndex((item) => item.id === server);
  if (index >= 0) return index;
  let hash = 0;
  for (const char of server) hash = (hash * 31 + char.charCodeAt(0)) | 0;
  return Math.abs(hash);
}

/** The first slot from the model's own on that is not taken (its own if all are). */
function freeSlot(server: string, taken: ReadonlySet<number>): number {
  const own = order(server) % HUES.length;
  for (let step = 0; step < HUES.length; step++) {
    const slot = (own + step) % HUES.length;
    if (!taken.has(slot)) return slot;
  }
  return own;
}

/** Slots of the models with jobs, the queue's first, each group in server-list order: different
 * while there are no more models than hues. */
const slots = computed(() => {
  const queued = new Set<string>();
  const rest = new Set<string>();
  for (const job of jobs.value.values()) {
    if (job.deleted_at) continue;
    (isPending(job) ? queued : rest).add(job.server);
  }
  const byOrder = (a: string, b: string) => order(a) - order(b);
  const result = new Map<string, number>();
  const taken = new Set<number>();
  for (const server of [
    ...[...queued].sort(byOrder),
    ...[...rest].filter((server) => !queued.has(server)).sort(byOrder),
  ]) {
    const slot = freeSlot(server, taken);
    result.set(server, slot);
    taken.add(slot);
  }
  return { result, taken };
});

export function modelHue(server: string): number {
  const { result, taken } = slots.value;
  return HUES[result.get(server) ?? freeSlot(server, taken)] ?? 0;
}

/** Sets the model's hue for the chip, a row's stripe or anything else styled with --hue. */
export const modelStyle = (server: string) =>
  ({ "--hue": String(modelHue(server)) }) as CSSProperties;

export const modelTitle = (server: string) =>
  serverById(server)?.title ?? server;

export function ModelChip({ server }: { server: string }) {
  return (
    <span class="model-chip" style={modelStyle(server)} title={server}>
      <span class="dot" aria-hidden="true" />
      <span class="name">{modelTitle(server)}</span>
    </span>
  );
}
