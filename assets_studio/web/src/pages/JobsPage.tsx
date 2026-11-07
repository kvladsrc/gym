// Every model's jobs: the queue of every project (waiting and running, in the order they will run;
// the GPU is shared) and the open project's history of finished ones, newest first, filtered by
// outcome.
import { useMemo, useState } from "preact/hooks";

import type { Job } from "../api/client";
import { JobRow } from "../components/JobList";
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

export function JobsPage() {
  const [outcome, setOutcome] = useState<Outcome>("all");
  const all = useMemo(
    () => [...jobs.value.values()].filter((job) => !job.deleted_at),
    [jobs.value],
  );
  const queue = all
    .filter(isPending)
    .sort((a: Job, b: Job) => (a.created_at < b.created_at ? -1 : 1));
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
    <div class="jobs-page">
      <section class="jobs" aria-label={t("jobsPage.queue")}>
        <h2>
          {t("jobsPage.queue")} <span class="hint">{queue.length}</span>
        </h2>
        {queue.length ? (
          <ul>
            {queue.map((job) => (
              <JobRow key={job.id} job={job} wide />
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
              <JobRow key={job.id} job={job} wide />
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
  );
}
