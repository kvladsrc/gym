import type { Job } from "../api/client";

export interface Binding {
  node: string;
  output_index: number;
}
interface Base {
  id: string;
  title: string;
  x: number;
  y: number;
}
export interface AssetNode extends Base {
  kind: "asset";
  asset_id: string;
}
export interface TaskNode extends Base {
  kind: "task";
  server: string;
  task: string;
  prompt: string | null;
  params: Record<string, unknown>;
  count: number;
  seed: number | null;
  inputs: Record<string, Binding>;
}
export type FlowNode = AssetNode | TaskNode;
export interface Graph {
  schema_version: 1;
  title: string;
  nodes: FlowNode[];
}
export interface FlowRecord {
  id: string;
  revision: number;
  graph: Graph;
  created_at: string;
  updated_at: string;
}
export interface Run {
  id: string;
  flow_id: string;
  revision: number;
  created_at: string;
  graph: Graph;
  state: string;
  status?: string;
  error: string | null;
  jobs: Record<string, string>;
  node_jobs?: Record<string, Job & { status: string }>;
}

export async function request<T>(
  path: string,
  method = "GET",
  body?: unknown,
): Promise<T> {
  const init: RequestInit = { method };
  if (body !== undefined) {
    init.body = JSON.stringify(body);
    init.headers = { "Content-Type": "application/json" };
  }
  const response = await fetch(`/flow-api${path}`, init);
  if (!response.ok) {
    let message = `${response.status} ${response.statusText}`;
    try {
      const error = (await response.json()) as { detail?: unknown };
      if (typeof error.detail === "string") message = error.detail;
      else if (error.detail) message = JSON.stringify(error.detail);
    } catch {
      /* The server may return a non-JSON error. */
    }
    throw new Error(message);
  }
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}
