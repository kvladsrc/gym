// The chosen model: what it is (source, revision, licence, the server's technical details)
// and the history of its jobs for this task, as a plain list.
import { Fragment } from "preact";
import { useMemo } from "preact/hooks";

import type { Server, Task } from "../api/client";
import { has, t } from "../i18n";
import { JobList } from "./JobList";
import { jobsOfSection } from "../state/sections";
import { assets, jobs } from "../state/store";

const stateTitle = (state: string) => {
  const key = `state.${state}`;
  return has(key) ? t(key) : state;
};

export function ModelPanel({
  sectionId,
  server,
  task,
}: {
  sectionId: string;
  server: Server;
  task: Task;
}) {
  const history = useMemo(
    () =>
      jobsOfSection(sectionId).filter(
        (job) => job.server === server.id && job.task === task.task,
      ),
    // assets: a deletion can drop a job from the list
    [jobs.value, assets.value, sectionId, server.id, task.task],
  );
  const model = server.model;
  const details = Object.entries(model?.details ?? {});
  return (
    <aside class="model-panel" aria-label={t("info.label")}>
      <h2>{server.title}</h2>
      <dl class="meta">
        <dt>{t("info.state")}</dt>
        <dd>
          <span class={`dot ${server.state}`} /> {stateTitle(server.state)}
          {server.message ? ` · ${server.message}` : ""}
        </dd>
        {model && (
          <>
            <dt>{t("info.name")}</dt>
            <dd>{model.name}</dd>
            {model.revision && (
              <>
                <dt>{t("info.revision")}</dt>
                <dd class="mono">{model.revision}</dd>
              </>
            )}
            {model.license && (
              <>
                <dt>{t("info.license")}</dt>
                <dd>{model.license}</dd>
              </>
            )}
            {model.source && (
              <>
                <dt>{t("info.source")}</dt>
                <dd>
                  <a href={model.source} target="_blank" rel="noreferrer">
                    {model.source.replace(/^https?:\/\//, "")}
                  </a>
                </dd>
              </>
            )}
          </>
        )}
        {details.map(([label, value]) => (
          <Fragment key={label}>
            <dt>{label}</dt>
            <dd>{value}</dd>
          </Fragment>
        ))}
        <dt>{t("info.server")}</dt>
        <dd class="mono">{server.url}</dd>
      </dl>
      {!model && <p class="hint">{t("info.unknown")}</p>}
      <JobList jobs={history} />
    </aside>
  );
}
