// Tags as chips (ADR-007): each removable, a "+ tag" button opens a field
// (Enter adds, Esc or leaving it cancels). Clicking a chip's name can pick it,
// e.g. to filter the library by it.
import { useEffect, useRef, useState } from "preact/hooks";

import { t } from "../i18n";

/** "Style:Cartoon " → "style:cartoon"; empty stays empty. */
export const cleanTag = (tag: string) => tag.trim().toLowerCase();

export function Tags({
  tags,
  onChange,
  onPick,
  label,
}: {
  tags: readonly string[];
  onChange: (tags: string[]) => void;
  onPick?: (tag: string) => void;
  label: string;
}) {
  const [adding, setAdding] = useState(false);
  const [text, setText] = useState("");
  const field = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (adding) field.current?.focus();
  }, [adding]);

  function add() {
    const tag = cleanTag(text);
    if (tag && !tags.includes(tag)) onChange([...tags, tag].sort());
    setText("");
    setAdding(false);
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
        <input
          ref={field}
          type="text"
          class="tag-input"
          aria-label={t("tags.add")}
          placeholder={t("tags.placeholder")}
          value={text}
          onInput={(event) => setText(event.currentTarget.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter") add();
            if (event.key === "Escape") {
              setText("");
              setAdding(false);
            }
          }}
          onBlur={() => {
            setText("");
            setAdding(false);
          }}
        />
      ) : (
        <button class="tag-add" onClick={() => setAdding(true)}>
          {t("tags.add")}
        </button>
      )}
    </div>
  );
}
