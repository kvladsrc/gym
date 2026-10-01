// Form drafts, one per section. They live outside the components, so switching
// sections keeps what was typed, and "→ section" is a single action.
import { signal } from "@preact/signals";

import type { Asset, Server, Task } from "../api/client";
import { defaults, type Params, type Texts } from "../components/ParamsForm";
import { inputKind } from "../format";
import { go, hrefSection } from "./router";
import { type Section, serversFor, taskOf } from "./sections";

export interface Draft {
  task: string | null;
  /** The model the user chose; null: the first ready one, as it changes. */
  server: string | null;
  prompt: string;
  inputs: Record<string, string>; // role -> asset id
  params: Params;
  /** Number fields as typed; a text that is not a valid value blocks sending. */
  texts: Texts;
  /** Whether "Дополнительно" is open. */
  advanced: boolean;
  count: number;
  seed: string; // as typed; empty means random
  /** The parameter schema the params were made for; another model resets them. */
  schemaKey: string;
}

/** What the form submits to: a task of a server. */
export interface Target {
  task: Task;
  server: Server;
}

export const drafts = signal<ReadonlyMap<string, Draft>>(new Map());
/** The job shown in each section (null: the newest). */
export const shownJob = signal<ReadonlyMap<string, string>>(new Map());

const schemaKey = (task: Task | undefined) =>
  task ? JSON.stringify(task.params_schema) : "";

/** A server for the task: the one asked for if it can do it, else the first
 * ready one, else the first. */
function pickServer(
  section: Section,
  taskName: string,
  wanted: string | null,
): Server | undefined {
  const able = serversFor(section, taskName);
  return (
    able.find((server) => server.id === wanted) ??
    able.find((server) => server.state === "ready") ??
    able[0]
  );
}

export function targetOf(section: Section, draft: Draft): Target | undefined {
  const taskName = draft.task ?? section.tasks[0];
  if (!taskName) return undefined;
  const server = pickServer(section, taskName, draft.server);
  const task = server && taskOf(server, taskName);
  return server && task ? { task, server } : undefined;
}

function fresh(target: Target | undefined, keep?: Partial<Draft>): Draft {
  return {
    task: target?.task.task ?? null,
    server: null,
    prompt: "",
    inputs: {},
    params: target ? defaults(target.task.params_schema) : {},
    texts: {},
    advanced: false,
    count: 1,
    seed: "",
    schemaKey: schemaKey(target?.task),
    ...keep,
  };
}

/** The section's draft, fitted to its tasks, servers and parameter schema. */
export function draftFor(section: Section): Draft {
  const stored = drafts.value.get(section.id);
  const wanted: Draft = stored ?? fresh(undefined);
  const taskName = section.tasks.includes(wanted.task ?? "")
    ? wanted.task
    : (section.tasks[0] ?? null);
  const target = taskName
    ? targetOf(section, { ...wanted, task: taskName })
    : undefined;
  if (!stored) return fresh(target);
  if (stored.task !== taskName) {
    // The task disappeared: the text stays, inputs and parameters do not.
    return fresh(target, {
      prompt: stored.prompt,
      count: stored.count,
      seed: stored.seed,
    });
  }
  if (stored.schemaKey !== schemaKey(target?.task)) {
    // Another model for the same task: its own parameters; the inputs it
    // takes and the count it allows stay.
    const roles = new Set(target?.task.inputs.map((spec) => spec.role));
    return fresh(target, {
      server: stored.server,
      prompt: stored.prompt,
      inputs: Object.fromEntries(
        Object.entries(stored.inputs).filter(([role]) => roles.has(role)),
      ),
      count: Math.min(stored.count, target?.task.max_count ?? 1),
      seed: stored.seed,
      advanced: stored.advanced,
    });
  }
  // Models with the same parameters can still differ in variants and inputs.
  const maxCount = target?.task.max_count ?? 1;
  return stored.count > maxCount ? { ...stored, count: maxCount } : stored;
}

function save(sectionId: string, draft: Draft) {
  const next = new Map(drafts.value);
  next.set(sectionId, draft);
  drafts.value = next;
}

export function updateDraft(section: Section, change: Partial<Draft>) {
  const current = draftFor(section);
  // Tuned parameters belong to one model: from the first change on, the
  // automatic pick stays put, or another model becoming ready would reset them.
  const server =
    current.server ??
    ("params" in change
      ? (targetOf(section, current)?.server.id ?? null)
      : null);
  save(section.id, { ...current, ...change, server });
}

/** Switch the form to another task: its own inputs and parameters; the text stays. */
export function chooseTask(section: Section, taskName: string) {
  const current = draftFor(section);
  const target = targetOf(section, { ...current, task: taskName });
  save(
    section.id,
    fresh(target, {
      // A model chosen by hand stays chosen if it can do this task too.
      server: current.server,
      prompt: current.prompt,
      seed: current.seed,
      advanced: current.advanced,
    }),
  );
}

/** Switch the model; the task, text and inputs stay. The choice sticks:
 * jobs then wait for this model rather than go to another. */
export function chooseServer(section: Section, serverId: string) {
  save(section.id, { ...draftFor(section), server: serverId });
}

export function showJob(sectionId: string, jobId: string | null) {
  const next = new Map(shownJob.value);
  if (jobId) next.set(sectionId, jobId);
  else next.delete(sectionId);
  shownJob.value = next;
}

/** Tasks of a section that accept this asset as an input, with the input role. */
export function acceptingTasks(section: Section, asset: Asset) {
  return section.tasks.flatMap((taskName) => {
    const server = pickServer(section, taskName, null);
    const task = server && taskOf(server, taskName);
    return (task?.inputs ?? [])
      .filter((spec) => inputKind(spec) === asset.kind)
      .map((spec) => ({ task: taskName, role: spec.role }));
  });
}

/** Open a section with the asset as an input. If the task already chosen there
 * takes the asset, only its input changes (tuned parameters stay: iterating
 * image-to-image keeps its strength); otherwise the first task that takes it. */
export function sendTo(section: Section, asset: Asset) {
  const accepting = acceptingTasks(section, asset);
  const current = draftFor(section);
  const same = accepting.find((item) => item.task === current.task);
  if (same) {
    save(section.id, {
      ...current,
      inputs: { ...current.inputs, [same.role]: asset.id },
    });
    go(hrefSection(section.id));
    return;
  }
  const [first] = accepting;
  if (!first) return;
  save(
    section.id,
    fresh(targetOf(section, { ...current, task: first.task }), {
      server: current.server,
      prompt: current.prompt,
      seed: current.seed,
      advanced: current.advanced,
      inputs: { [first.role]: asset.id },
    }),
  );
  go(hrefSection(section.id));
}
