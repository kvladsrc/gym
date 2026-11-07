// The projects (ADR-010): open one, rename it, create one, delete an empty one. Files are moved
// between projects in the library's details panel.
import { useEffect, useState } from "preact/hooks";

import { api, type Project } from "../api/client";
import { hrefLibrary } from "../state/router";
import {
  currentProject,
  DEFAULT_PROJECT,
  loadProjects,
  openProject,
  projects,
} from "../state/projects";
import { attempt, say } from "../state/store";
import { t } from "../i18n";

function open(id: string) {
  openProject(id);
  location.hash = hrefLibrary();
}

function ProjectRow({ project }: { project: Project }) {
  const [title, setTitle] = useState(project.title);
  useEffect(() => setTitle(project.title), [project.title]);
  const current = project.id === currentProject.value;
  const rename = async () => {
    const next = title.trim();
    if (!next || next === project.title) {
      setTitle(project.title);
      return;
    }
    if (await attempt(() => api.renameProject(project.id, next)))
      await attempt(loadProjects);
    else setTitle(project.title);
  };
  const remove = async () => {
    if (await attempt(() => api.deleteProject(project.id))) {
      say(t("projects.deleted", { title: project.title }));
      await attempt(loadProjects);
    }
  };
  const deletable = project.id !== DEFAULT_PROJECT && !project.asset_count;
  return (
    <li
      class={`project-row${current ? " current" : ""}`}
      data-project={project.id}
    >
      <input
        type="text"
        aria-label={t("projects.rename")}
        value={title}
        maxLength={100}
        onInput={(event) => setTitle(event.currentTarget.value)}
        onBlur={() => void rename()}
        onKeyDown={(event) => {
          if (event.key === "Enter") event.currentTarget.blur();
          if (event.key === "Escape") {
            setTitle(project.title);
            event.currentTarget.blur();
          }
        }}
      />
      <code class="project-id">{project.id}</code>
      <span class="hint">
        {t("project.assets", { n: project.asset_count })}
      </span>
      <button
        class={`button small${current ? "" : " primary"}`}
        onClick={() => open(project.id)}
      >
        {current ? t("projects.opened") : t("projects.open")}
      </button>
      <button
        class="button small danger"
        disabled={!deletable}
        title={deletable ? undefined : t("projects.deleteTitle")}
        onClick={() => void remove()}
      >
        {t("projects.delete")}
      </button>
    </li>
  );
}

export function ProjectsPage() {
  const [title, setTitle] = useState("");
  // Fresh counts on every visit: the list in the top bar is not refreshed per new file.
  useEffect(() => void attempt(loadProjects), []);
  const create = async (event: Event) => {
    event.preventDefault();
    if (!title.trim()) return;
    const project = await attempt(() => api.createProject(title.trim()));
    if (!project) return;
    setTitle("");
    await attempt(loadProjects);
    open(project.id);
  };
  return (
    <div class="jobs-page projects-page">
      <section class="jobs" aria-label={t("projects.heading")}>
        <h2>{t("projects.heading")}</h2>
        <p class="hint">{t("projects.about")}</p>
        <ul class="project-list">
          {projects.value.map((project) => (
            <ProjectRow key={project.id} project={project} />
          ))}
        </ul>
        <form class="row project-new" onSubmit={(event) => void create(event)}>
          <input
            type="text"
            placeholder={t("projects.new")}
            aria-label={t("projects.new")}
            value={title}
            maxLength={100}
            onInput={(event) => setTitle(event.currentTarget.value)}
          />
          <button class="button primary" type="submit" disabled={!title.trim()}>
            {t("projects.create")}
          </button>
        </form>
      </section>
    </div>
  );
}
