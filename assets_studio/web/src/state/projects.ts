// Projects (ADR-010): every asset and job belongs to one. The open project is what the library,
// the history and the sections show, and where new jobs and uploads go. It is remembered in
// this browser; the queue stays shared, as the GPU is.
import { computed, signal } from "@preact/signals";

import { api, type Project } from "../api/client";

export const DEFAULT_PROJECT = "default";
const KEY = "assets-studio.project";

function remembered(): string {
  try {
    return localStorage.getItem(KEY) || DEFAULT_PROJECT;
  } catch {
    return DEFAULT_PROJECT; // storage blocked
  }
}

export const projects = signal<Project[]>([]);
/** The open project's id. */
export const currentProject = signal<string>(remembered());
/** True once the list has arrived and the open project is known to exist. */
export const projectsLoaded = computed(() =>
  projects.value.some((project) => project.id === currentProject.value),
);

export function openProject(id: string) {
  currentProject.value = id;
  try {
    localStorage.setItem(KEY, id);
  } catch {
    // storage blocked: the choice lasts until the page is reloaded
  }
}

/** Reload the list; a project deleted meanwhile (or in another browser) closes. */
export async function loadProjects() {
  const list = await api.projects();
  projects.value = list;
  if (!list.some((project) => project.id === currentProject.value))
    openProject(DEFAULT_PROJECT);
}

export const projectTitle = (id: string) =>
  projects.value.find((project) => project.id === id)?.title ?? id;
