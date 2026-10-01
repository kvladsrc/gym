// The selected job: its state while it runs, then its candidates and actions.
import { useEffect, useState } from "preact/hooks";

import { api, type Job, type Server } from "../api/client";
import { duration, statusLabel, taskLabel } from "../format";
import { assetById, attempt, isPending, mergeJob } from "../state/store";
import { AssetActions } from "./AssetActions";
import { AssetPreview } from "./AssetPreview";
import { AssetThumb } from "./AssetThumb";

function useNow(active: boolean) {
  const [now, setNow] = useState(Date.now());
  useEffect(() => {
    if (!active) return;
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [active]);
  return now;
}

function Progress({ job, server }: { job: Job; server: Server | undefined }) {
  const now = useNow(true);
  const since = job.started_at ?? job.created_at;
  const elapsed = duration((now - new Date(since).getTime()) / 1000);
  const text =
    job.status === "running"
      ? `Генерация… ${elapsed}`
      : job.status === "waiting_model"
        ? server?.state === "unavailable"
          ? `Ждёт запуска модели ${server.title}: ${server.url}`
          : "Ждёт, пока модель освободится"
        : Object.keys(job.dependencies).length
          ? "Ждёт результата предыдущего задания"
          : "В очереди";
  return (
    <div class="placeholder">
      <div class="row" style={{ justifyContent: "center" }}>
        <span class="spinner" />
        <span>{text}</span>
      </div>
    </div>
  );
}

/** ``onShow(jobId)`` switches the view to another job (e.g. a retry). */
export function JobView({
  job,
  server,
  onShow,
}: {
  job: Job;
  server: Server | undefined;
  onShow: (jobId: string) => void;
}) {
  const [selected, setSelected] = useState(0);
  useEffect(() => setSelected(0), [job.id]);
  const assetId = job.outputs[selected] ?? job.outputs[0];
  const asset = assetId ? assetById(assetId) : undefined;

  return (
    <>
      <div class="job-head">
        <h2>{taskLabel(job.task)}</h2>
        <span class={`badge ${job.status}`}>{statusLabel(job)}</span>
        {job.prompt && (
          <span class="prompt" title={job.prompt}>
            {job.prompt}
          </span>
        )}
      </div>

      {job.status === "failed" && (
        <div class="error-box">
          <b>{job.error_code}</b>: {job.error_message}
        </div>
      )}

      {(isPending(job) || job.outputs.length > 0) && (
        <div class="preview">
          {isPending(job) ? (
            <Progress job={job} server={server} />
          ) : asset?.deleted_at ? (
            <div class="placeholder">Удалено из библиотеки.</div>
          ) : asset ? (
            <AssetPreview key={asset.id} asset={asset} />
          ) : (
            <span class="spinner" />
          )}
        </div>
      )}

      {job.outputs.length > 1 && (
        <div class="candidates">
          {job.outputs.map((id, index) => (
            <button
              key={id}
              class={`thumb${index === selected ? " selected" : ""}`}
              aria-label={`Вариант ${index + 1}`}
              aria-pressed={index === selected}
              onClick={() => setSelected(index)}
            >
              <AssetThumb asset={assetById(id)} />
            </button>
          ))}
        </div>
      )}

      <div class="actions">
        {asset && !asset.deleted_at && <AssetActions asset={asset} />}
        {(job.status === "queued" || job.status === "waiting_model") && (
          <button
            class="button small"
            onClick={() =>
              void attempt(() => api.cancelJob(job.id)).then(
                (done) => done && mergeJob(done),
              )
            }
          >
            Отменить
          </button>
        )}
        {(job.status === "failed" || job.status === "cancelled") && (
          <button
            class="button small"
            onClick={() =>
              void attempt(() => api.retryJob(job.id)).then((created) => {
                if (!created) return;
                mergeJob(created);
                onShow(created.id);
              })
            }
          >
            Повторить
          </button>
        )}
        <span class="hint">
          {job.seed !== null && `seed ${job.seed}`}
          {job.timing &&
            typeof job.timing.generate_s === "number" &&
            ` · ${duration(job.timing.generate_s)}`}
          {job.model_snapshot && ` · ${String(job.model_snapshot.name)}`}
        </span>
      </div>
    </>
  );
}
