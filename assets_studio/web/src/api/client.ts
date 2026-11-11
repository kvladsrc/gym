// Typed access to the studio API. Types come from the generated OpenAPI schema.
import type { components } from "./schema";

type Schemas = components["schemas"];
export type Server = Schemas["ServerOut"];
export type Task = Schemas["TaskInfo"];
export type InputSpec = Schemas["InputSpec"];
export type Job = Schemas["JobOut"];
export type JobCreate = Schemas["JobCreate"];

/** GET /api/assets/{id}/skeleton: a native motion as joint positions per frame. */
export interface Skeleton {
  format: string;
  fps: number;
  /** Every stride-th frame of the stored motion is shown. */
  stride: number;
  frames: number;
  source_frames: number;
  names: string[];
  parents: number[];
  /** Per frame, x, y, z of each joint in turn; metres, Y up. */
  positions: number[][];
}
export type Asset = Schemas["AssetOut"];
export type Project = Schemas["ProjectOut"];
export type Parent = Schemas["ParentOut"];

export class ApiError extends Error {}

async function request<T>(
  method: string,
  path: string,
  body?: BodyInit,
  type?: string,
): Promise<T> {
  const headers: Record<string, string> = type ? { "Content-Type": type } : {};
  const init: RequestInit = { method, headers };
  if (body !== undefined) init.body = body;
  const response = await fetch(path, init);
  if (!response.ok) {
    let message = `${response.status} ${response.statusText}`;
    try {
      const detail = ((await response.json()) as { detail?: unknown }).detail;
      if (typeof detail === "string") message = detail;
    } catch {
      // Not JSON: keep the status line.
    }
    throw new ApiError(message);
  }
  return (await response.json()) as T;
}

const json = (value: unknown) => JSON.stringify(value);

export const api = {
  servers: () => request<Server[]>("GET", "/api/servers"),
  projects: () => request<Project[]>("GET", "/api/projects"),
  createProject: (title: string) =>
    request<Project>(
      "POST",
      "/api/projects",
      json({ title }),
      "application/json",
    ),
  renameProject: (id: string, title: string) =>
    request<Project>(
      "PATCH",
      `/api/projects/${encodeURIComponent(id)}`,
      json({ title }),
      "application/json",
    ),
  deleteProject: (id: string) =>
    request<Project>("DELETE", `/api/projects/${encodeURIComponent(id)}`),
  /** Newest first: of one project, or of every project when none is given. */
  jobs: (
    query: { project?: string; status?: string[]; limit?: number } = {},
  ) => {
    const params = new URLSearchParams();
    if (query.project) params.set("project", query.project);
    for (const status of query.status ?? []) params.append("status", status);
    params.set("limit", String(query.limit ?? 300));
    return request<Job[]>("GET", `/api/jobs?${params.toString()}`);
  },
  job: (id: string) => request<Job>("GET", `/api/jobs/${id}`),
  createJob: (body: JobCreate) =>
    request<Job>("POST", "/api/jobs", json(body), "application/json"),
  cancelJob: (id: string) => request<Job>("POST", `/api/jobs/${id}/cancel`),
  retryJob: (id: string) => request<Job>("POST", `/api/jobs/${id}/retry`),
  deleteJob: (id: string) => request<Job>("DELETE", `/api/jobs/${id}`),
  assets: (query: {
    /** Of this project; of every project when absent. */
    project?: string | undefined;
    kind?: string;
    favorite?: boolean;
    tags?: string[];
    minRating?: number;
    unrated?: boolean;
    /** Made by a motion task (true) or not (false), whatever the file format. */
    motion?: boolean;
    limit?: number;
  }) => {
    const params = new URLSearchParams();
    if (query.project) params.set("project", query.project);
    if (query.kind) params.set("kind", query.kind);
    if (query.favorite !== undefined)
      params.set("favorite", String(query.favorite));
    for (const tag of query.tags ?? []) params.append("tag", tag);
    if (query.minRating !== undefined)
      params.set("min_rating", String(query.minRating));
    if (query.unrated) params.set("unrated", "true");
    if (query.motion !== undefined) params.set("motion", String(query.motion));
    params.set("limit", String(query.limit ?? 300));
    return request<Asset[]>("GET", `/api/assets?${params.toString()}`);
  },
  asset: (id: string) => request<Asset>("GET", `/api/assets/${id}`),
  skeleton: (id: string) =>
    request<Skeleton>("GET", `/api/assets/${id}/skeleton`),
  saveVideoFrame: (id: string, seconds: number) =>
    request<Asset>(
      "POST",
      `/api/assets/${id}/frames?time_s=${seconds.toFixed(3)}`,
    ),
  updateAsset: (
    id: string,
    body: {
      title?: string;
      favorite?: boolean;
      rating?: number | null;
      tags?: string[];
      /** Moves the asset to this project. */
      project?: string;
    },
  ) =>
    request<Asset>(
      "PATCH",
      `/api/assets/${id}`,
      json(body),
      "application/json",
    ),
  upload: (file: Blob, project: string, title?: string) => {
    const params = new URLSearchParams({ project });
    if (title) params.set("title", title);
    return request<Asset>(
      "POST",
      `/api/assets?${params.toString()}`,
      file,
      "application/octet-stream",
    );
  },
  importUrl: (url: string, project: string) =>
    request<Asset>(
      "POST",
      "/api/assets/import-url",
      json({ url, project }),
      "application/json",
    ),
  deleteAsset: (id: string) => request<Asset>("DELETE", `/api/assets/${id}`),
  /** Keeps a mesh's thumbnail picture (PNG or WebP), drawn by the web UI's
   * renderer of this version (ADR-011). */
  putPreview: (id: string, picture: Blob, version: number) =>
    request<Asset>(
      "PUT",
      `/api/assets/${id}/preview?version=${version}`,
      picture,
      "application/octet-stream",
    ),
  lineage: (id: string) =>
    request<Parent[]>("GET", `/api/assets/${id}/lineage`),
  /** The job that made the asset; null for an imported file. */
  assetJob: (id: string) => request<Job | null>("GET", `/api/assets/${id}/job`),
};
