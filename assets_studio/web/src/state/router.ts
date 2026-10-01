// Hash routes: #/<section> (image, mesh, audio, text, video), #/library,
// #/library/<asset id>.
import { signal } from "@preact/signals";

export type Route =
  | { page: "section"; sectionId: string }
  | { page: "library"; assetId: string | null }
  | { page: "home" };

function parse(hash: string): Route {
  const [page, id] = hash.replace(/^#\/?/, "").split("/");
  if (page === "library")
    return { page: "library", assetId: id ? decodeURIComponent(id) : null };
  if (page) return { page: "section", sectionId: decodeURIComponent(page) };
  return { page: "home" };
}

export const route = signal<Route>(parse(window.location.hash));
window.addEventListener(
  "hashchange",
  () => (route.value = parse(window.location.hash)),
);

export const hrefSection = (sectionId: string) =>
  `#/${encodeURIComponent(sectionId)}`;
export const hrefLibrary = (assetId?: string) =>
  assetId ? `#/library/${encodeURIComponent(assetId)}` : "#/library";

export function go(href: string) {
  window.location.hash = href;
}
