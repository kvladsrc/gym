// Making things: the tree of models on the left, the chosen model's task form in the middle,
// the model's description and its job history on the right.
import type { Server } from "../api/client";
import { GenerationForm } from "../components/GenerationForm";
import { ModelPanel } from "../components/ModelPanel";
import { ModelTree } from "../components/ModelTree";
import { taskLabel } from "../format";
import { draftFor, targetOf } from "../state/drafts";
import type { Section } from "../state/sections";
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
  const target = targetOf(section, draftFor(section));
  return (
    <div class="workspace">
      <ModelTree />
      <section class="launch panel" aria-label={t("form.label")}>
        {target ? (
          <>
            <header class="launch-head">
              <h1>{taskLabel(target.task.task)}</h1>
              <span class="hint">
                {section.title} · {target.server.title}
              </span>
            </header>
            <ServerHint server={target.server} />
            <GenerationForm section={section} />
          </>
        ) : (
          <p class="hint">{t("form.unknownTasks")}</p>
        )}
      </section>
      {target && (
        <ModelPanel
          sectionId={section.id}
          server={target.server}
          task={target.task}
        />
      )}
    </div>
  );
}
