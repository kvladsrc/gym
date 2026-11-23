// Tags as chips (ADR-007): each removable, a "+ tag" button opens a field
// (Enter adds, Esc or leaving it cancels). Clicking a chip's name can pick it,
// e.g. to filter the library by it. The field can list the tags that exist,
// most used first, narrowed by what is typed: a click or the arrows and Enter
// take one.
import { useEffect, useId, useRef, useState } from "preact/hooks";

import type { TagCount } from "../api/client";
import { t } from "../i18n";

/** "Style:Cartoon " → "style:cartoon"; empty stays empty. */
export const cleanTag = (tag: string) => tag.trim().toLowerCase();

export function Tags({
  tags,
  onChange,
  onPick,
  label,
  known,
}: {
  tags: readonly string[];
  onChange: (tags: string[]) => void;
  onPick?: (tag: string) => void;
  label: string;
  /** Loads the tags to list under the field, e.g. every tag of the library. */
  known?: () => Promise<readonly TagCount[]>;
}) {
  const [adding, setAdding] = useState(false);
  const [text, setText] = useState("");
  const [offered, setOffered] = useState<readonly TagCount[]>([]);
  // The listed tag the arrows have reached; -1: what is typed.
  const [active, setActive] = useState(-1);
  const field = useRef<HTMLInputElement>(null);
  const list = useRef<HTMLUListElement>(null);
  const listId = useId();
  useEffect(() => {
    if (!adding) return;
    field.current?.focus();
    if (!known) return;
    // Loaded on every opening: tags come and go with assets.
    let current = true;
    known().then(
      (found) => {
        // Most used first; the API's order by name stays among equals.
        if (current) setOffered([...found].sort((a, b) => b.count - a.count));
      },
      // A hint only: without the list the field still takes any tag.
      () => undefined,
    );
    return () => {
      current = false;
    };
  }, [adding]);
  useEffect(() => {
    list.current?.children[active]?.scrollIntoView({ block: "nearest" });
  }, [active]);

  const typed = cleanTag(text);
  const listed = offered.filter(
    (item) => !tags.includes(item.tag) && item.tag.includes(typed),
  );

  function close() {
    setText("");
    setOffered([]);
    setActive(-1);
    setAdding(false);
  }

  function add(tag: string) {
    if (tag && !tags.includes(tag)) onChange([...tags, tag].sort());
    close();
  }

  return (
    <div class="tags" role="group" aria-label={label}>
      {tags.map((tag) => (
        <span key={tag} class="tag">
          {onPick ? (
            <button
              class="tag-name"
              title={t("tags.pick", { tag })}
              onClick={() => onPick(tag)}
            >
              {tag}
            </button>
          ) : (
            <span class="tag-name">{tag}</span>
          )}
          <button
            class="tag-remove"
            aria-label={t("tags.remove", { tag })}
            onClick={() => onChange(tags.filter((other) => other !== tag))}
          >
            ×
          </button>
        </span>
      ))}
      {adding ? (
        <span class="tag-entry">
          <input
            ref={field}
            type="text"
            class="tag-input"
            role={known ? "combobox" : undefined}
            aria-expanded={known ? listed.length > 0 : undefined}
            aria-controls={known ? listId : undefined}
            aria-activedescendant={
              active >= 0 ? `${listId}-${active}` : undefined
            }
            aria-label={t("tags.add")}
            placeholder={t("tags.placeholder")}
            value={text}
            onInput={(event) => {
              setText(event.currentTarget.value);
              setActive(-1);
            }}
            onKeyDown={(event) => {
              if (event.key === "Enter") add(listed[active]?.tag ?? typed);
              if (event.key === "Escape") close();
              if (event.key === "ArrowDown" && listed.length) {
                event.preventDefault();
                setActive(Math.min(active + 1, listed.length - 1));
              }
              if (event.key === "ArrowUp" && listed.length) {
                event.preventDefault();
                setActive(Math.max(active - 1, -1));
              }
            }}
            onBlur={close}
          />
          {listed.length > 0 && (
            <ul
              ref={list}
              id={listId}
              class="tag-options"
              role="listbox"
              aria-label={t("tags.known")}
              // Keep the field focused: leaving it closes the list before the click.
              onMouseDown={(event) => event.preventDefault()}
            >
              {listed.map((item, index) => (
                <li
                  key={item.tag}
                  id={`${listId}-${index}`}
                  role="option"
                  aria-selected={index === active}
                  class={index === active ? "active" : ""}
                  onClick={() => add(item.tag)}
                >
                  <span>{item.tag}</span>
                  <span class="tag-count">{item.count}</span>
                </li>
              ))}
            </ul>
          )}
        </span>
      ) : (
        <button class="tag-add" onClick={() => setAdding(true)}>
          {t("tags.add")}
        </button>
      )}
    </div>
  );
}
