// The jobs of one model and task as a plain list: what runs, what failed and why, and where a
// finished result is. Results themselves are looked at in the library.
import { useEffect, useState } from "preact/hooks";

import { api, type Job } from "../api/client";
import { duration, statusLabel, taskLabel, time } from "../format";
import { hrefLibrary } from "../state/router";
import { serverById } from "../state/sections";
import { attempt, isPending, mergeJob } from "../state/store";
import { t } from "../i18n";

const SHOWN = 30;

function useNow(active: boolean) {
  const [now, setNow] = useState(Date.now());
  useEffect(() => {
    if (!active) return;
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [active]);
  return now;
}

function waiting(job: Job, elapsed: string) {
  const server = serverById(job.server);
  if (job.status === "running") return t("job.generating", { elapsed });
  if (job.status === "waiting_model")
    return server?.state === "unavailable"
      ? t("job.waitingStart", { model: server.title, url: server.url })
      : t("job.waitingFree");
  return Object.keys(job.dependencies).length
    ? t("job.waitingInput")
    : t("status.queued");
}

/** One job. ``wide``: also its task and model, for the list of every job. */
export function JobRow({ job, wide = false }: { job: Job; wide?: boolean }) {
  const pending = isPending(job);
  const now = useNow(pending);
  const elapsed = duration(
    (now - new Date(job.started_at ?? job.created_at).getTime()) / 1000,
  );
  const took =
    job.timing && typeof job.timing.generate_s === "number"
      ? duration(job.timing.generate_s)
      : "";
  const first = job.outputs[0];
  const act = (call: () => Promise<Job>) =>
    void attempt(call).then((done) => done && mergeJob(done));
  return (
    <li class={`job-row ${job.status}`} data-job={job.id} data-task={job.task}>
      <div class="job-line">
        <span class={`badge ${job.status}`}>{statusLabel(job)}</span>
        <span class="job-time">{time(job.created_at)}</span>
        {wide && <span class="job-task">{taskLabel(job.task)}</span>}
        {wide && (
          <span class="job-model">
            {serverById(job.server)?.title ?? job.server}
          </span>
        )}
        <span class="job-time">{pending ? elapsed : took}</span>
      </div>
      {job.prompt && (
        <div class="prompt" title={job.prompt}>
          {job.prompt}
        </div>
      )}
      {pending && <div class="job-note">{waiting(job, elapsed)}</div>}
      {job.status === "failed" && (
        <div class="error-box">
          <b>{job.error_code}</b>: {job.error_message}
        </div>
      )}
      <div class="job-actions">
        {job.status === "succeeded" && first && (
          <a href={hrefLibrary(first)}>
            {job.outputs.length > 1
              ? t("jobs.openMany", { n: job.outputs.length })
              : t("jobs.open")}
          </a>
        )}
        {(job.status === "queued" || job.status === "waiting_model") && (
          <button class="link" onClick={() => act(() => api.cancelJob(job.id))}>
            {t("job.cancel")}
          </button>
        )}
        {(job.status === "failed" || job.status === "cancelled") && (
          <>
            <button
              class="link"
              onClick={() => act(() => api.retryJob(job.id))}
            >
              {t("job.retry")}
            </button>
            <button
              class="link"
              onClick={() => act(() => api.deleteJob(job.id))}
            >
              {t("job.delete")}
            </button>
          </>
        )}
      </div>
    </li>
  );
}

export function JobList({ jobs }: { jobs: Job[] }) {
  return (
    <section class="jobs" aria-label={t("jobs.title")}>
      <h3>{t("jobs.title")}</h3>
      {jobs.length ? (
        <ul>
          {jobs.slice(0, SHOWN).map((job) => (
            <JobRow key={job.id} job={job} />
          ))}
        </ul>
      ) : (
        <p class="hint">{t("jobs.empty")}</p>
      )}
      {jobs.length > SHOWN && (
        <p class="hint">{t("jobs.more", { n: jobs.length - SHOWN })}</p>
      )}
    </section>
  );
}
