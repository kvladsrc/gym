// Everything about one job, beside the list of jobs: what it was asked (model, task, prompt,
// every parameter with the defaults, seed, inputs and the jobs it waits for), how it went
// (waiting, running, error, times) and what it made, with the whole job as JSON to copy.
import { Fragment } from "preact";
import { useEffect, useState } from "preact/hooks";

import { api, type Job } from "../api/client";
import {
  assetName,
  duration,
  exactTime,
  secondsBetween,
  statusLabel,
  taskLabel,
} from "../format";
import { localized, t } from "../i18n";
import { projectTitle } from "../state/projects";
import { go, hrefJob, hrefJobs, hrefLibrary } from "../state/router";
import { serverById, taskOf } from "../state/sections";
import { assetById, isPending, jobs, mergeJob } from "../state/store";
import { AssetThumb } from "./AssetThumb";
import { act, useNow, waiting } from "./JobList";
import { ModelChip } from "./ModelChip";
import { CopyButton, Params, request } from "./Provenance";

/** A job from the list, or loaded when it is not there (another project's, an old one); null
 * when the studio does not know it. Once loaded it is kept current by the events. */
function useJob(id: string): Job | null | undefined {
  const cached = jobs.value.get(id);
  const [missing, setMissing] = useState<string | null>(null);
  const known = cached !== undefined;
  useEffect(() => {
    if (known) return;
    let current = true;
    api.job(id).then(
      (found) => current && mergeJob(found),
      () => current && setMissing(id),
    );
    return () => {
      current = false;
    };
  }, [id, known]);
  if (cached) return cached;
  return missing === id ? null : undefined;
}

/** An input's name as the model's task describes it. */
function roleLabel(job: Job, role: string) {
  const server = serverById(job.server);
  const spec =
    server &&
    taskOf(server, job.task)?.inputs.find((input) => input.role === role);
  return localized(spec?.labels, role);
}

function AssetInput({ role, assetId }: { role: string; assetId: string }) {
  const asset = assetById(assetId);
  return (
    <li class="job-input">
      <a class="thumb" href={hrefLibrary(assetId)} aria-label={role}>
        <AssetThumb asset={asset} />
      </a>
      <div class="job-input-text">
        <b>{role}</b>
        <span class="hint">{asset ? assetName(asset) : assetId}</span>
      </div>
    </li>
  );
}

/** An input still to come from another job, or that came from one. */
function JobInput({
  role,
  jobId,
  output,
}: {
  role: string;
  jobId: string;
  output: number;
}) {
  const source = useJob(jobId);
  const made = source?.outputs[output];
  return (
    <li class="job-input">
      {made ? (
        <a class="thumb" href={hrefLibrary(made)} aria-label={role}>
          <AssetThumb asset={assetById(made)} />
        </a>
      ) : (
        <span class="thumb empty" aria-hidden="true">
          …
        </span>
      )}
      <div class="job-input-text">
        <b>{role}</b>
        {source ? (
          <a class="job-link" href={hrefJob(jobId)}>
            <ModelChip server={source.server} />
            <span>{taskLabel(source.task)}</span>
            <span class={`badge ${source.status}`}>{statusLabel(source)}</span>
          </a>
        ) : (
          <a class="hint mono" href={hrefJob(jobId)}>
            {jobId}
          </a>
        )}
        {source && source.count > 1 && (
          <span class="hint">
            {t("jobDetails.output", { n: output + 1, of: source.count })}
          </span>
        )}
      </div>
    </li>
  );
}

/** The model at run time (the job's snapshot), or the server's current one before. */
function ModelInfo({ job }: { job: Job }) {
  const snapshot = job.model_snapshot;
  const model: Record<string, unknown> | null =
    snapshot ?? serverById(job.server)?.model ?? null;
  if (!model) return null;
  const text = (key: string) => {
    const value = model[key];
    return typeof value === "string" ? value : null;
  };
  const details =
    model.details && typeof model.details === "object"
      ? Object.entries(model.details as Record<string, unknown>)
      : [];
  const source = text("source");
  return (
    <details class="job-model-info">
      <summary>
        {snapshot ? t("jobDetails.modelRun") : t("jobDetails.modelNow")}:{" "}
        {text("name") ?? job.server}
        {text("revision") && ` · ${text("revision")}`}
      </summary>
      <dl class="meta">
        <dt>{t("info.server")}</dt>
        <dd class="mono">{job.server}</dd>
        {text("license") && (
          <>
            <dt>{t("info.license")}</dt>
            <dd>{text("license")}</dd>
          </>
        )}
        {source && (
          <>
            <dt>{t("info.source")}</dt>
            <dd>
              <a href={source} target="_blank" rel="noreferrer">
                {source.replace(/^https?:\/\//, "")}
              </a>
            </dd>
          </>
        )}
        {details.map(([label, value]) => (
          <Fragment key={label}>
            <dt>{label}</dt>
            <dd>{typeof value === "string" ? value : JSON.stringify(value)}</dd>
          </Fragment>
        ))}
      </dl>
    </details>
  );
}

/** Times measured by the model server, by their own names (generate_s, …). */
function timingEntries(job: Job): [string, string][] {
  return Object.entries(job.timing ?? {}).map(([name, value]) => [
    name,
    typeof value === "number" && name.endsWith("_s")
      ? duration(value)
      : typeof value === "string"
        ? value
        : JSON.stringify(value),
  ]);
}

function Actions({ job }: { job: Job }) {
  const first = job.outputs[0];
  return (
    <div class="actions">
      {job.status === "succeeded" && first && (
        <a class="button small" href={hrefLibrary(first)}>
          {job.outputs.length > 1
            ? t("jobs.openMany", { n: job.outputs.length })
            : t("jobs.open")}
        </a>
      )}
      {(job.status === "queued" || job.status === "waiting_model") && (
        <button
          class="button small"
          onClick={() => void act(() => api.cancelJob(job.id))}
        >
          {t("job.cancel")}
        </button>
      )}
      {(job.status === "failed" || job.status === "cancelled") && (
        <>
          <button
            class="button small"
            onClick={() =>
              void act(() => api.retryJob(job.id)).then(
                (retry) => retry && go(hrefJob(retry.id)),
              )
            }
          >
            {t("job.retry")}
          </button>
          <button
            class="button small"
            onClick={() =>
              void act(() => api.deleteJob(job.id)).then(
                (deleted) => deleted && go(hrefJobs),
              )
            }
          >
            {t("job.delete")}
          </button>
        </>
      )}
      <CopyButton
        value={JSON.stringify(request(job), null, 2)}
        label={t("provenance.copyAll")}
      />
    </div>
  );
}

function Body({ job }: { job: Job }) {
  const pending = isPending(job);
  const now = useNow(pending);
  const elapsed = duration(
    secondsBetween(job.started_at ?? job.created_at, now),
  );
  const inputs = Object.entries(job.inputs);
  const dependencies = Object.entries(job.dependencies);
  return (
    <>
      <h2 class="job-details-title">{taskLabel(job.task)}</h2>
      {pending && <p class="job-note">{waiting(job, elapsed)}</p>}
      {job.deleted_at && (
        <p class="hint warn">
          {t("jobDetails.deleted", { time: exactTime(job.deleted_at) })}
        </p>
      )}
      {job.status === "failed" && (
        <div class="error-box job-error">
          <b>{job.error_code}</b>: {job.error_message}
        </div>
      )}
      <Actions job={job} />
      {job.outputs.length > 0 && (
        <div class="field">
          <span>{t("jobDetails.outputs", { n: job.outputs.length })}</span>
          <div class="candidates">
            {job.outputs.map((id) => {
              const asset = assetById(id);
              return (
                <a
                  key={id}
                  class="thumb"
                  href={hrefLibrary(id)}
                  title={asset ? assetName(asset) : id}
                >
                  <AssetThumb asset={asset} />
                </a>
              );
            })}
          </div>
        </div>
      )}
      {job.prompt !== null && (
        <div class="field">
          <span>{t("provenance.prompt")}</span>
          <pre class="prompt-text" tabIndex={0}>
            {job.prompt}
          </pre>
          <div class="row">
            <CopyButton value={job.prompt} label={t("provenance.copyPrompt")} />
          </div>
        </div>
      )}
      {(inputs.length > 0 || dependencies.length > 0) && (
        <div class="field">
          <span>{t("jobDetails.inputs")}</span>
          <ul class="job-inputs">
            {inputs.map(([role, assetId]) => (
              <AssetInput
                key={role}
                role={roleLabel(job, role)}
                assetId={assetId}
              />
            ))}
            {dependencies.map(([role, dependency]) => (
              <JobInput
                key={role}
                role={roleLabel(job, role)}
                jobId={dependency.job_id}
                output={dependency.output_index}
              />
            ))}
          </ul>
        </div>
      )}
      <div class="field">
        <span>{t("provenance.params")}</span>
        <Params job={job} />
      </div>
      <dl class="meta">
        <dt>{t("jobDetails.project")}</dt>
        <dd>{projectTitle(job.project)}</dd>
        <dt>Seed</dt>
        <dd>{job.seed ?? "—"}</dd>
        {job.count > 1 && (
          <>
            <dt>{t("jobDetails.count")}</dt>
            <dd>{job.count}</dd>
          </>
        )}
        <dt>{t("jobDetails.created")}</dt>
        <dd>{exactTime(job.created_at)}</dd>
        {(job.started_at || pending) && (
          <>
            <dt>{t("jobDetails.waited")}</dt>
            <dd>
              {duration(secondsBetween(job.created_at, job.started_at ?? now))}
            </dd>
          </>
        )}
        {job.started_at && (
          <>
            <dt>{t("jobDetails.started")}</dt>
            <dd>{exactTime(job.started_at)}</dd>
            <dt>{t("jobDetails.ran")}</dt>
            <dd>
              {duration(secondsBetween(job.started_at, job.finished_at ?? now))}
            </dd>
          </>
        )}
        {job.finished_at && (
          <>
            <dt>{t("jobDetails.finished")}</dt>
            <dd>{exactTime(job.finished_at)}</dd>
          </>
        )}
        {timingEntries(job).map(([name, value]) => (
          <Fragment key={name}>
            <dt class="mono">{name}</dt>
            <dd>{value}</dd>
          </Fragment>
        ))}
        {job.retryable_failures > 0 && (
          <>
            <dt>{t("jobDetails.retries")}</dt>
            <dd>{job.retryable_failures}</dd>
          </>
        )}
        {job.retry_of && (
          <>
            <dt>{t("jobDetails.retryOf")}</dt>
            <dd>
              <a class="mono" href={hrefJob(job.retry_of)}>
                {job.retry_of}
              </a>
            </dd>
          </>
        )}
        {job.idempotency_key && (
          <>
            <dt>{t("jobDetails.idempotency")}</dt>
            <dd class="mono">{job.idempotency_key}</dd>
          </>
        )}
        <dt>ID</dt>
        <dd class="mono">{job.id}</dd>
      </dl>
      <ModelInfo job={job} />
      <details class="job-json">
        <summary>{t("jobDetails.json")}</summary>
        <pre class="prompt-text mono" tabIndex={0}>
          {JSON.stringify(job, null, 2)}
        </pre>
        <div class="row">
          <CopyButton
            value={JSON.stringify(job, null, 2)}
            label={t("jobDetails.copyJson")}
          />
        </div>
      </details>
    </>
  );
}

export function JobDetails({ jobId }: { jobId: string }) {
  const job = useJob(jobId);
  // Escape closes the details, unless it is meant for a field.
  useEffect(() => {
    const close = (event: KeyboardEvent) => {
      const target = event.target as Element | null;
      if (event.key === "Escape" && !target?.closest("input, textarea, select"))
        go(hrefJobs);
    };
    window.addEventListener("keydown", close);
    return () => window.removeEventListener("keydown", close);
  }, []);
  return (
    <aside class="job-details" aria-label={t("jobDetails.title")}>
      <div class="job-details-head">
        {job && <ModelChip server={job.server} />}
        {job && <span class={`badge ${job.status}`}>{statusLabel(job)}</span>}
        <a
          class="close"
          href={hrefJobs}
          title={t("jobDetails.close")}
          aria-label={t("jobDetails.close")}
        >
          ×
        </a>
      </div>
      {job === undefined ? (
        <span class="spinner" />
      ) : job === null ? (
        <p class="hint">{t("jobDetails.notFound", { id: jobId })}</p>
      ) : (
        <Body job={job} />
      )}
    </aside>
  );
}
