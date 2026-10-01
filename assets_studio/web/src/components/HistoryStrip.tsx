// The section's jobs, newest first; clicking one shows it.
import type { Job } from "../api/client";
import { statusLabel, taskLabel } from "../format";
import { assetById, isPending } from "../state/store";
import { AssetThumb } from "./AssetThumb";
import { AlertIcon } from "./Icons";

export function HistoryStrip({
  jobs,
  selected,
  onSelect,
}: {
  jobs: Job[];
  selected: string | null;
  onSelect: (jobId: string) => void;
}) {
  if (!jobs.length)
    return (
      <div class="history">
        <span class="hint">История этого раздела появится здесь.</span>
      </div>
    );
  return (
    <div class="history">
      {jobs.map((job) => {
        const first = job.outputs[0];
        const state = isPending(job)
          ? " pending"
          : job.status === "failed" || job.status === "cancelled"
            ? " failed"
            : "";
        return (
          <button
            key={job.id}
            class={`thumb${state}${job.id === selected ? " selected" : ""}`}
            title={`${taskLabel(job.task)} · ${statusLabel(job)}${job.prompt ? ` · ${job.prompt}` : ""}`}
            onClick={() => onSelect(job.id)}
          >
            {first ? (
              <AssetThumb asset={assetById(first)} />
            ) : isPending(job) ? (
              <span class="spinner" />
            ) : (
              <AlertIcon />
            )}
          </button>
        );
      })}
    </div>
  );
}
