// How an asset was made: the job behind it with everything it was given —
// model, task, prompt, every parameter (defaults included), seed and inputs —
// to read, compare or copy for a new job.
import { Fragment } from "preact";
import { useEffect, useState } from "preact/hooks";

import { api, type Asset, type Job } from "../api/client";
import { duration, taskLabel, time } from "../format";
import { localized, t } from "../i18n";
import { attempt, servers } from "../state/store";
import { properties } from "./ParamsForm";

type Copied = "no" | "yes" | "failed";

function useCopy(): [Copied, (value: string) => void] {
  const [copied, setCopied] = useState<Copied>("no");
  function copy(value: string) {
    try {
      // navigator.clipboard is undefined on pages that are not a secure
      // context (the types say otherwise), and the write can be denied.
      void navigator.clipboard.writeText(value).then(
        () => setCopied("yes"),
        () => setCopied("failed"),
      );
    } catch {
      setCopied("failed");
    }
  }
  return [copied, copy];
}

function CopyButton({ value, label }: { value: string; label: string }) {
  const [copied, copy] = useCopy();
  return (
    <button class="button small" onClick={() => copy(value)}>
      {copied === "yes"
        ? t("preview.copied")
        : copied === "failed"
          ? t("preview.copyFailed")
          : label}
    </button>
  );
}

const show = (value: unknown) =>
  typeof value === "string" ? value : JSON.stringify(value);

/** The parameters the model ran with: what the job asked for, completed with
 * the server's defaults (effective_params); the ones set by hand are marked. */
function Params({ job }: { job: Job }) {
  const values = job.effective_params ?? job.params;
  const names = Object.keys(values);
  if (!names.length) return null;
  const schema = servers.value
    .find((server) => server.id === job.server)
    ?.tasks.find((task) => task.task === job.task)?.params_schema;
  const labels = new Map(
    schema
      ? properties(schema).map(([name, prop]) => [
          name,
          localized(prop["x-labels"], prop.description ?? prop.title ?? name),
        ])
      : [],
  );
  return (
    <dl class="meta params">
      {names.map((name) => (
        <Fragment key={name}>
          <dt title={labels.get(name)}>{name}</dt>
          <dd>
            {name in job.params ? (
              <b>{show(values[name])}</b>
            ) : (
              show(values[name])
            )}
          </dd>
        </Fragment>
      ))}
    </dl>
  );
}

/** What is needed to make the asset again: the request of its job. */
function request(job: Job) {
  return {
    server: job.server,
    task: job.task,
    ...(job.prompt !== null && { prompt: job.prompt }),
    params: job.effective_params ?? job.params,
    seed: job.seed,
    inputs: job.inputs,
    ...(Object.keys(job.dependencies).length && {
      dependencies: job.dependencies,
    }),
    model: job.model_snapshot,
    job: job.id,
  };
}

export function Provenance({ asset }: { asset: Asset }) {
  const [job, setJob] = useState<Job | null | undefined>(undefined);
  useEffect(() => {
    let current = true;
    setJob(undefined);
    void attempt(() => api.assetJob(asset.id)).then(
      (found) => current && setJob(found ?? null),
    );
    return () => {
      current = false;
    };
  }, [asset.id]);
  if (!job) return null;
  const model = job.model_snapshot;
  const position = job.outputs.indexOf(asset.id);
  return (
    <section class="provenance" aria-label={t("provenance.title")}>
      <h3>{t("provenance.title")}</h3>
      <dl class="meta">
        <dt>{t("library.model")}</dt>
        <dd>
          {typeof model?.name === "string" ? model.name : job.server}
          {typeof model?.revision === "string" && ` · ${model.revision}`}
        </dd>
        <dt>{t("provenance.task")}</dt>
        <dd>{taskLabel(job.task)}</dd>
        <dt>Seed</dt>
        <dd>
          {typeof asset.meta.seed === "number" ? asset.meta.seed : job.seed}
          {job.outputs.length > 1 &&
            ` · ${t("provenance.variant", { n: position + 1, of: job.outputs.length })}`}
        </dd>
        <dt>{t("provenance.queued")}</dt>
        <dd>
          {time(job.created_at)}
          {job.timing &&
            typeof job.timing.generate_s === "number" &&
            ` · ${duration(job.timing.generate_s)}`}
        </dd>
      </dl>
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
      <div class="field">
        <span>{t("provenance.params")}</span>
        <Params job={job} />
      </div>
      <div class="row">
        <CopyButton
          value={JSON.stringify(request(job), null, 2)}
          label={t("provenance.copyAll")}
        />
      </div>
    </section>
  );
}
