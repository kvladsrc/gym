// The jobs of one model and task as a plain list: what runs, what failed and why, and where a
// finished result is. Results themselves are looked at in the library; everything else about a
// job is in its details on the jobs page.
import { useEffect, useState } from "preact/hooks";

import { api, type Job } from "../api/client";
import { duration, statusLabel, taskLabel, time } from "../format";
import { currentProject, projectTitle } from "../state/projects";
import { go, hrefJob, hrefLibrary } from "../state/router";
import { serverById } from "../state/sections";
import { attempt, isPending, mergeJob } from "../state/store";
import { t } from "../i18n";
import { ModelChip, modelStyle } from "./ModelChip";

const SHOWN = 30;

/** The current time, ticking every second while ``active``. */
export function useNow(active: boolean) {
  const [now, setNow] = useState(Date.now());
  useEffect(() => {
    if (!active) return;
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [active]);
  return now;
}

/** What an unfinished job is waiting for. */
export function waiting(job: Job, elapsed: string) {
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

/** Cancel, retry or delete, keeping the list current; resolves to the job the server returned
 * (a retry returns the new job). */
export async function act(call: () => Promise<Job>) {
  const done = await attempt(call);
  if (done) mergeJob(done);
  return done;
}

/** A click on the row itself, not on its links or buttons, nor the end of a text selection. */
function opensRow(event: MouseEvent) {
  const target = event.target as Element | null;
  if (target?.closest("a, button")) return false;
  return !window.getSelection()?.toString();
}

/** One job. ``wide``: also its model and task, and its project when not the open one, for the
 * list of every job; there the row opens the job's details (``selected``: they are open). */
export function JobRow({
  job,
  wide = false,
  selected = false,
}: {
  job: Job;
  wide?: boolean;
  selected?: boolean;
}) {
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
  const classes = ["job-row", job.status];
  if (wide) classes.push("model-row");
  if (selected) classes.push("selected");
  return (
    <li
      class={classes.join(" ")}
      data-job={job.id}
      data-task={job.task}
      style={wide ? modelStyle(job.server) : undefined}
      onClick={
        wide
          ? (event) => {
              if (opensRow(event)) go(hrefJob(job.id));
            }
          : undefined
      }
    >
      <div class="job-line">
        {wide && <ModelChip server={job.server} />}
        {wide && (
          <a
            class="job-task"
            href={hrefJob(job.id)}
            aria-current={selected ? "true" : undefined}
          >
            {taskLabel(job.task)}
          </a>
        )}
        <span class={`badge ${job.status}`}>{statusLabel(job)}</span>
        {wide && job.project !== currentProject.value && (
          <span class="job-project">
            {t("project.other", { title: projectTitle(job.project) })}
          </span>
        )}
        <span class="job-time">{time(job.created_at)}</span>
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
          <button
            class="link"
            onClick={() => void act(() => api.cancelJob(job.id))}
          >
            {t("job.cancel")}
          </button>
        )}
        {(job.status === "failed" || job.status === "cancelled") && (
          <>
            <button
              class="link"
              onClick={() => void act(() => api.retryJob(job.id))}
            >
              {t("job.retry")}
            </button>
            <button
              class="link"
              onClick={() => void act(() => api.deleteJob(job.id))}
            >
              {t("job.delete")}
            </button>
          </>
        )}
        {!wide && <a href={hrefJob(job.id)}>{t("job.details")}</a>}
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
