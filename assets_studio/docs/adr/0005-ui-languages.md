# ADR-005. Bilingual UI; contract 1.2: parameter labels

Status: accepted (M8).

## Context

Studio's UI is Russian-only. M8 item 2 requests English with a language switch.
UI strings belong to the web client, but parameter labels (“Size”, “Steps”,
“Style”) come from server schema `description` fields (ADR-001), currently in
Russian. Agents also read them through MCP and understand English better.

## Decision

### UI

Strings live in `web/src/i18n.ts` dictionaries (`ru`, `en`), keyed by English
identifiers: `t("key", {parameters})`. Language is a signal selected in the top
bar and remembered in the browser; default to Russian for Russian browsers,
otherwise English. Update `<html lang>` with it. Task/section/state names,
units and dates also come from dictionaries.

### Contract 1.2: parameter labels

- Parameter schema `description` is **English**, used by agents (MCP, REST)
  and the English UI.
- Optional `"x-labels": {"ru": "Размер"}` supplies other-language labels. UI
  fallback order: `x-labels[language]`, `description`, `title`, parameter name.
- This extends schemas like `x-primary` (ADR-003): servers without it remain
  compatible; protocol `contract` does not change.
- SDK `ui(ru="Размер", primary=True)` builds `json_schema_extra`, replacing
  direct `PRIMARY` usage (retained for compatibility). Non-string `x-labels`
  values fail SDK validation.

All repository servers migrate to English `description` with Russian `x-labels`.

## Consequences

- Studio/server errors stay English (as they already are); only UI is translated.
- User-written prompts and asset titles are not translated.

## Alternatives

- **`Accept-Language` on `/v1/info`** would return client-language schemas.
  Studio caches `/v1/info` (ADR-004), requiring one copy per language, while
  agents would receive unpredictable languages.
- **Translate parameter names in Studio:** a dictionary would need to know
  every model's parameters; new servers would show raw names until Studio changed.
