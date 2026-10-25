// Hash routes: #/<section> (image, mesh, animation, audio, text, video), #/library,
// #/library/<asset id>, #/jobs (the queue and the history of every model), #/tool/<id> (the
// studio's own tools, no model: tool/frame saves a video frame as an image).
import { signal } from "@preact/signals";

export type Route =
  | { page: "section"; sectionId: string }
  | { page: "library"; assetId: string | null }
  | { page: "jobs" }
  | { page: "tool"; toolId: string }
  | { page: "home" };

function parse(hash: string): Route {
  const [page, id] = hash.replace(/^#\/?/, "").split("/");
  if (page === "jobs") return { page: "jobs" };
  if (page === "tool") return { page: "tool", toolId: id ?? "" };
  if (page === "library")
    return { page: "library", assetId: id ? decodeURIComponent(id) : null };
  if (page) return { page: "section", sectionId: decodeURIComponent(page) };
  return { page: "home" };
}

export const route = signal<Route>(parse(window.location.hash));
/** The section last open, for the Generate link while the library is shown. */
export const lastSection = signal<string | null>(
  route.value.page === "section" ? route.value.sectionId : null,
);
window.addEventListener("hashchange", () => {
  route.value = parse(window.location.hash);
  if (route.value.page === "section") lastSection.value = route.value.sectionId;
});

export const hrefSection = (sectionId: string) =>
  `#/${encodeURIComponent(sectionId)}`;
export const hrefJobs = "#/jobs";
export const hrefTool = (toolId: string) =>
  `#/tool/${encodeURIComponent(toolId)}`;
export const hrefLibrary = (assetId?: string) =>
  assetId ? `#/library/${encodeURIComponent(assetId)}` : "#/library";

export function go(href: string) {
  window.location.hash = href;
}
