// Sections (ADR-004): one per kind of result, built from the tasks the model
// servers declare. Inside a section the user picks a task and a model.
import { computed } from "@preact/signals";

import type { Job, Server, Task } from "../api/client";
import { assetById, jobs, servers } from "./store";
import { type Key, t } from "../i18n";

export interface Section {
  id: string;
  title: string;
  /** Task names in the order servers declare them. */
  tasks: string[];
  /** Servers with at least one task here. */
  servers: Server[];
}

const SECTIONS: [string, Key][] = [
  ["image", "section.image"],
  ["mesh", "section.mesh"],
  ["audio", "section.audio"],
  ["text", "section.text"],
  ["video", "section.video"],
];

const MIME_KINDS: Record<string, string> = {
  image: "image",
  model: "mesh",
  audio: "audio",
  text: "text",
  video: "video",
};

/** The section of a task: the kind of what it produces. */
export function sectionOfTask(task: Task): string | undefined {
  const type = task.output_mime[0]?.split("/")[0];
  return type ? MIME_KINDS[type] : undefined;
}

export const sections = computed<Section[]>(() =>
  SECTIONS.flatMap(([id, titleKey]) => {
    const tasks: string[] = [];
    const members: Server[] = [];
    for (const server of servers.value) {
      const here = server.tasks.filter((task) => sectionOfTask(task) === id);
      if (here.length) members.push(server);
      for (const task of here)
        if (!tasks.includes(task.task)) tasks.push(task.task);
    }
    return tasks.length
      ? [{ id, title: t(titleKey), tasks, servers: members }]
      : [];
  }),
);

/** Servers configured but never seen: their tasks, hence sections, are unknown. */
export const unseenServers = computed(() =>
  servers.value.filter((server) => !server.seen),
);

export const sectionById = (id: string) =>
  sections.value.find((section) => section.id === id);

export const serverById = (id: string) =>
  servers.value.find((server) => server.id === id);

export const taskOf = (server: Server, taskName: string) =>
  server.tasks.find((task) => task.task === taskName);

/** Servers of a section that can do this task. */
export const serversFor = (section: Section, taskName: string) =>
  section.servers.filter((server) => taskOf(server, taskName));

const RANK: Record<string, number> = {
  ready: 0,
  busy: 1,
  loading: 2,
  error: 3,
  unavailable: 4,
};

/** The most usable state among servers: what a section's dot shows. */
export function bestState(members: Server[]): string {
  return (
    [...members].sort((a, b) => (RANK[a.state] ?? 9) - (RANK[b.state] ?? 9))[0]
      ?.state ?? "unavailable"
  );
}

// What a task produces, by its name, for tasks no current server declares
// (a model was swapped on the same port, a server left the config).
const RESULTS: Record<string, string> = {
  image: "image",
  "3d": "mesh",
  speech: "audio",
  audio: "audio",
  text: "text",
  video: "video",
};

/** The section a job belongs to: by what its task produces on its server,
 * else by the task's name ("…-to-image"), so past jobs keep their place. */
export function sectionOfJob(job: Job): string | undefined {
  const server = serverById(job.server);
  const task = server && taskOf(server, job.task);
  if (task) return sectionOfTask(task);
  const result = job.task.split("-to-").at(-1);
  return result ? RESULTS[result] : undefined;
}

/** A job with results, every one of them deleted from the library. Results not
 * loaded yet count as kept; checking stops at the first kept one, so only the
 * results after deleted ones are loaded. */
export const allResultsDeleted = (job: Job) =>
  job.outputs.length > 0 &&
  job.outputs.every((id) => assetById(id)?.deleted_at);

/** Jobs of a section, newest first (ids are time-ordered); deleted jobs and
 * jobs whose results were all deleted are left out. */
export const jobsOfSection = (sectionId: string) =>
  [...jobs.value.values()]
    .filter(
      (job) =>
        sectionOfJob(job) === sectionId &&
        !job.deleted_at &&
        !allResultsDeleted(job),
    )
    .sort((a, b) => (a.id < b.id ? 1 : -1));
