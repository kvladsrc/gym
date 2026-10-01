// A section: the generation form on the left, the shown job in the centre,
// the section's history below.
import { useMemo } from "preact/hooks";

import type { Server } from "../api/client";
import { GenerationForm } from "../components/GenerationForm";
import { HistoryStrip } from "../components/HistoryStrip";
import { JobView } from "../components/JobView";
import { draftFor, shownJob, showJob, targetOf } from "../state/drafts";
import { jobsOfSection, type Section, serverById } from "../state/sections";
import { jobs, servers } from "../state/store";

function ServerHint({ server }: { server: Server }) {
  if (server.state === "ready" || server.state === "busy") return null;
  const text =
    server.state === "loading"
      ? `${server.title} загружается. Задания подождут.`
      : server.state === "error"
        ? `${server.title} не загрузилась: ${server.message ?? ""}`
        : `${server.title}: сервер не запущен (${server.url}). Задания подождут его запуска.`;
  return (
    <p class={`hint ${server.state === "error" ? "error" : "warn"}`}>{text}</p>
  );
}

export function SectionPage({ section }: { section: Section }) {
  const history = useMemo(
    () => jobsOfSection(section.id),
    [jobs.value, servers.value, section.id],
  );
  const selected = shownJob.value.get(section.id);
  const shown = (selected ? jobs.value.get(selected) : undefined) ?? history[0];
  const show = (jobId: string) => showJob(section.id, jobId);
  const target = targetOf(section, draftFor(section));

  return (
    <div class="workspace">
      <aside class="panel">
        {target && <ServerHint server={target.server} />}
        <GenerationForm section={section} onSubmitted={show} />
      </aside>
      <section class="stage">
        {shown ? (
          <JobView
            job={shown}
            server={serverById(shown.server)}
            onShow={show}
          />
        ) : (
          <div class="placeholder">Здесь появится результат.</div>
        )}
      </section>
      <HistoryStrip
        jobs={history}
        selected={shown?.id ?? null}
        onSelect={show}
      />
    </div>
  );
}
