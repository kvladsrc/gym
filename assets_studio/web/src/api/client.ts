// Typed access to the studio API. Types come from the generated OpenAPI schema.
import type { components } from "./schema";

type Schemas = components["schemas"];
export type Server = Schemas["ServerOut"];
export type Task = Schemas["TaskInfo"];
export type InputSpec = Schemas["InputSpec"];
export type Job = Schemas["JobOut"];
export type JobCreate = Schemas["JobCreate"];
export type Asset = Schemas["AssetOut"];
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
  jobs: (limit = 300) => request<Job[]>("GET", `/api/jobs?limit=${limit}`),
  job: (id: string) => request<Job>("GET", `/api/jobs/${id}`),
  createJob: (body: JobCreate) =>
    request<Job>("POST", "/api/jobs", json(body), "application/json"),
  cancelJob: (id: string) => request<Job>("POST", `/api/jobs/${id}/cancel`),
  retryJob: (id: string) => request<Job>("POST", `/api/jobs/${id}/retry`),
  deleteJob: (id: string) => request<Job>("DELETE", `/api/jobs/${id}`),
  assets: (query: {
    kind?: string;
    favorite?: boolean;
    tags?: string[];
    minRating?: number;
    unrated?: boolean;
    limit?: number;
  }) => {
    const params = new URLSearchParams();
    if (query.kind) params.set("kind", query.kind);
    if (query.favorite !== undefined)
      params.set("favorite", String(query.favorite));
    for (const tag of query.tags ?? []) params.append("tag", tag);
    if (query.minRating !== undefined)
      params.set("min_rating", String(query.minRating));
    if (query.unrated) params.set("unrated", "true");
    params.set("limit", String(query.limit ?? 300));
    return request<Asset[]>("GET", `/api/assets?${params.toString()}`);
  },
  asset: (id: string) => request<Asset>("GET", `/api/assets/${id}`),
  updateAsset: (
    id: string,
    body: {
      title?: string;
      favorite?: boolean;
      rating?: number | null;
      tags?: string[];
    },
  ) =>
    request<Asset>(
      "PATCH",
      `/api/assets/${id}`,
      json(body),
      "application/json",
    ),
  upload: (file: Blob, title?: string) =>
    request<Asset>(
      "POST",
      `/api/assets${title ? `?title=${encodeURIComponent(title)}` : ""}`,
      file,
      "application/octet-stream",
    ),
  importUrl: (url: string) =>
    request<Asset>(
      "POST",
      "/api/assets/import-url",
      json({ url }),
      "application/json",
    ),
  deleteAsset: (id: string) => request<Asset>("DELETE", `/api/assets/${id}`),
  lineage: (id: string) =>
    request<Parent[]>("GET", `/api/assets/${id}/lineage`),
};
