// A section: the generation form on the left, the shown job in the centre,
// the section's history below.
import { useEffect, useMemo } from "preact/hooks";

import type { Server } from "../api/client";
import { GenerationForm } from "../components/GenerationForm";
import { HistoryStrip } from "../components/HistoryStrip";
import { JobView } from "../components/JobView";
import { draftFor, shownJob, showJob, targetOf } from "../state/drafts";
import { jobsOfSection, type Section, serverById } from "../state/sections";
import { assets, jobs, servers } from "../state/store";
import { t } from "../i18n";

function ServerHint({ server }: { server: Server }) {
  if (server.state === "ready" || server.state === "busy") return null;
  const text =
    server.state === "loading"
      ? t("section.loading", { model: server.title })
      : server.state === "error"
        ? t("section.failed", {
            model: server.title,
            message: server.message ?? "",
          })
        : t("section.notRunning", { model: server.title, url: server.url });
  return (
    <p class={`hint ${server.state === "error" ? "error" : "warn"}`}>{text}</p>
  );
}

export function SectionPage({ section }: { section: Section }) {
  const history = useMemo(
    () => jobsOfSection(section.id),
    // assets: a deletion can drop a job from the history.
    [jobs.value, servers.value, assets.value, section.id],
  );
  const selected = shownJob.value.get(section.id);
  // A job whose results were all deleted has left the history: show the newest.
  const shown = history.find((job) => job.id === selected) ?? history[0];
  // Left the history, not just submitted and not here yet.
  const gone =
    selected !== undefined &&
    jobs.value.has(selected) &&
    !history.some((job) => job.id === selected);
  useEffect(() => {
    // Forget it, or restoring a result would bring the stage back to it.
    if (gone) showJob(section.id, null);
  }, [gone, section.id]);
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
          <div class="placeholder">{t("section.placeholder")}</div>
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
