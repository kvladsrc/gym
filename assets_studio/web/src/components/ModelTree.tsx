// Every model as a tree, like a file manager: section, task, model. Picking a model opens its
// task's form.
import { useState } from "preact/hooks";

import { taskLabel } from "../format";
import { chooseServer, chooseTask, draftFor, targetOf } from "../state/drafts";
import { go, hrefSection, hrefTool, route } from "../state/router";
import { type Section, sections, serversFor } from "../state/sections";
import { has, t } from "../i18n";

const stateTitle = (state: string) => {
  const key = `state.${state}`;
  return has(key) ? t(key) : state;
};

/** Open a model's task. The draft stays when the task stays (another model of it, or the same
 * one again); another task starts from its own defaults, keeping the text. */
function select(section: Section, task: string, serverId: string) {
  if (draftFor(section).task !== task) chooseTask(section, task);
  chooseServer(section, serverId);
  go(hrefSection(section.id));
}

export function ModelTree() {
  const [closed, setClosed] = useState<ReadonlySet<string>>(new Set());
  const current = route.value;
  const toggle = (id: string) => {
    const next = new Set(closed);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    setClosed(next);
  };
  return (
    <nav class="tree" aria-label={t("tree.label")}>
      <ul>
        {sections.value.map((section) => {
          const open = !closed.has(section.id);
          const here =
            current.page === "section" && current.sectionId === section.id;
          const target = here
            ? targetOf(section, draftFor(section))
            : undefined;
          return (
            <li key={section.id}>
              <button
                class="tree-folder"
                aria-expanded={open}
                onClick={() => toggle(section.id)}
              >
                <span class="caret" aria-hidden="true">
                  {open ? "▾" : "▸"}
                </span>
                {section.title}
              </button>
              {open && (
                <ul>
                  {section.id === "image" && (
                    <li>
                      <span class="tree-task">{t("tool.frameTask")}</span>
                      <ul>
                        <li>
                          <button
                            class={`tree-model${current.page === "tool" && current.toolId === "frame" ? " active" : ""}`}
                            aria-pressed={
                              current.page === "tool" &&
                              current.toolId === "frame"
                            }
                            onClick={() => go(hrefTool("frame"))}
                          >
                            <span class="dot ready" />
                            {t("tool.studio")}
                          </button>
                        </li>
                      </ul>
                    </li>
                  )}
                  {section.tasks.map((task) => (
                    <li key={task}>
                      <span class="tree-task">{taskLabel(task)}</span>
                      <ul>
                        {serversFor(section, task).map((server) => {
                          const active =
                            target?.task.task === task &&
                            target.server.id === server.id;
                          return (
                            <li key={server.id}>
                              <button
                                class={`tree-model${active ? " active" : ""}`}
                                aria-pressed={active}
                                title={`${server.title}: ${stateTitle(server.state)}`}
                                onClick={() => select(section, task, server.id)}
                              >
                                <span class={`dot ${server.state}`} />
                                {server.title}
                              </button>
                            </li>
                          );
                        })}
                      </ul>
                    </li>
                  ))}
                </ul>
              )}
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
