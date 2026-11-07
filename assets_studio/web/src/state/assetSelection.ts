import { useSignal } from "@preact/signals";
import { useEffect } from "preact/hooks";

/** Selection belongs to the current filters/project and only includes shown assets. */
export function useAssetSelection(shown: string[], scope: string) {
  const selection = useSignal({
    scope,
    ids: new Set<string>(),
    anchor: null as string | null,
  });
  useEffect(() => {
    if (selection.peek().scope !== scope)
      selection.value = { scope, ids: new Set(), anchor: null };
  }, [scope]);
  const current = selection.value;
  const ids = shown.filter(
    (id) => current.scope === scope && current.ids.has(id),
  );

  function clear() {
    selection.value = { scope, ids: new Set(), anchor: null };
  }

  function toggle(id: string, range: boolean) {
    const next = new Set(ids);
    const start = current.anchor ? shown.indexOf(current.anchor) : -1;
    const end = shown.indexOf(id);
    if (range && start >= 0 && end >= 0) {
      const interval = shown.slice(
        Math.min(start, end),
        Math.max(start, end) + 1,
      );
      for (const item of interval) next.add(item);
    } else if (next.has(id)) next.delete(id);
    else next.add(id);
    selection.value = {
      scope,
      ids: next,
      anchor: range && start >= 0 ? current.anchor : id,
    };
  }

  function remove(id: string) {
    const latest = selection.peek();
    const next = new Set(latest.ids);
    next.delete(id);
    selection.value = { ...latest, ids: next };
  }

  return {
    ids,
    clear,
    toggle,
    remove,
    all: () => {
      selection.value = {
        scope,
        ids: new Set(shown),
        anchor: shown[0] ?? null,
      };
    },
  };
}
