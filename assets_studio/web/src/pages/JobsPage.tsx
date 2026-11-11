// Every model's jobs: the queue of every project (running first, then waiting in the order they
// were queued; the GPU is shared) and the open project's history of finished ones, newest first,
// filtered by outcome. Each model has its colour; the queue's models are summed up as chips
// above it, and one of them narrows the queue to that model. A job opens its details beside
// the lists (#/jobs/<id>).
import { useMemo, useState } from "preact/hooks";

import type { Job } from "../api/client";
import { JobDetails } from "../components/JobDetails";
import { JobRow } from "../components/JobList";
import { modelStyle, modelTitle } from "../components/ModelChip";
import { currentProject } from "../state/projects";
import { isPending, jobs } from "../state/store";
import { type Key, t } from "../i18n";

const OUTCOMES = [
  { id: "all", label: "library.all" },
  { id: "succeeded", label: "status.succeeded" },
  { id: "failed", label: "status.failed" },
  { id: "cancelled", label: "status.cancelled" },
] as const satisfies readonly { id: string; label: Key }[];
type Outcome = (typeof OUTCOMES)[number]["id"];
const HISTORY = 200;

const byCreated = (a: Job, b: Job) => (a.created_at < b.created_at ? -1 : 1);

interface ModelCount {
  server: string;
  jobs: number;
  running: boolean;
}

/** The queue's models in the order the queue reaches them, with their number of jobs. */
function countByModel(queue: Job[]): ModelCount[] {
  const counts = new Map<string, ModelCount>();
  for (const job of queue) {
    const count = counts.get(job.server) ?? {
      server: job.server,
      jobs: 0,
      running: false,
    };
    count.jobs += 1;
    count.running ||= job.status === "running";
    counts.set(job.server, count);
  }
  return [...counts.values()];
}

function QueueModels({
  models,
  chosen,
  choose,
}: {
  models: ModelCount[];
  chosen: string | null;
  choose: (server: string | null) => void;
}) {
  return (
    <div class="queue-models" role="group" aria-label={t("jobsPage.byModel")}>
      {models.map((model) => {
        const active = model.server === chosen;
        return (
          <button
            key={model.server}
            class={`model-chip${active ? " active" : ""}${model.running ? " running" : ""}`}
            style={modelStyle(model.server)}
            aria-pressed={active}
            title={active ? t("jobsPage.allModels") : t("jobsPage.onlyModel")}
            onClick={() => choose(active ? null : model.server)}
          >
            <span class="dot" aria-hidden="true" />
            <span class="name">{modelTitle(model.server)}</span>
            <span class="count">{model.jobs}</span>
          </button>
        );
      })}
    </div>
  );
}

export function JobsPage({ jobId }: { jobId: string | null }) {
  const [outcome, setOutcome] = useState<Outcome>("all");
  const [model, setModel] = useState<string | null>(null);
  const all = useMemo(
    () => [...jobs.value.values()].filter((job) => !job.deleted_at),
    [jobs.value],
  );
  const pending = all.filter(isPending);
  const queue = [
    ...pending.filter((job) => job.status === "running").sort(byCreated),
    ...pending.filter((job) => job.status !== "running").sort(byCreated),
  ];
  const models = countByModel(queue);
  // A chosen model whose jobs have all left the queue no longer narrows it.
  const chosen = models.some((item) => item.server === model) ? model : null;
  const shown = chosen ? queue.filter((job) => job.server === chosen) : queue;
  const project = currentProject.value;
  const history = all
    .filter(
      (job) =>
        job.project === project &&
        !isPending(job) &&
        (outcome === "all" || job.status === outcome),
    )
    .sort((a: Job, b: Job) => (a.created_at < b.created_at ? 1 : -1));
  return (
    <div class={`jobs-page${jobId ? " with-details" : ""}`}>
      <div class="jobs-lists">
        <section class="jobs" aria-label={t("jobsPage.queue")}>
          <h2>
            {t("jobsPage.queue")} <span class="hint">{queue.length}</span>
          </h2>
          {models.length > 0 && (
            <QueueModels models={models} chosen={chosen} choose={setModel} />
          )}
          {queue.length ? (
            <ul>
              {shown.map((job) => (
                <JobRow
                  key={job.id}
                  job={job}
                  wide
                  selected={job.id === jobId}
                />
              ))}
            </ul>
          ) : (
            <p class="hint">{t("queue.empty")}</p>
          )}
        </section>
        <section class="jobs" aria-label={t("jobsPage.history")}>
          <div class="jobs-head">
            <h2>{t("jobsPage.history")}</h2>
            <div
              class="segmented"
              role="group"
              aria-label={t("jobsPage.outcome")}
            >
              {OUTCOMES.map((item) => (
                <button
                  key={item.id}
                  class={item.id === outcome ? "active" : ""}
                  aria-pressed={item.id === outcome}
                  onClick={() => setOutcome(item.id)}
                >
                  {t(item.label)}
                </button>
              ))}
            </div>
          </div>
          {history.length ? (
            <ul>
              {history.slice(0, HISTORY).map((job) => (
                <JobRow
                  key={job.id}
                  job={job}
                  wide
                  selected={job.id === jobId}
                />
              ))}
            </ul>
          ) : (
            <p class="hint">{t("jobsPage.noHistory")}</p>
          )}
          {history.length > HISTORY && (
            <p class="hint">
              {t("jobsPage.more", { n: history.length - HISTORY })}
            </p>
          )}
        </section>
      </div>
      {jobId && <JobDetails key={jobId} jobId={jobId} />}
    </div>
  );
}
