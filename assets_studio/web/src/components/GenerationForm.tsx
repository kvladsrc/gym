// The generation form of a section: task, model, inputs, description,
// variants and parameters. Its state is the section's draft in the store.
import { useState } from "preact/hooks";

import { api } from "../api/client";
import { taskLabel } from "../format";
import {
  chooseServer,
  chooseTask,
  draftFor,
  targetOf,
  updateDraft,
} from "../state/drafts";
import { type Section, serversFor } from "../state/sections";
import { assetById, mergeJob } from "../state/store";
import { InputPicker } from "./InputPicker";
import { invalidParams, isPrimary, ParamsForm, properties } from "./ParamsForm";

const SEED_LIMIT = 2 ** 32;
const MAX_SEGMENTS = 6;

function parseSeed(text: string): number | null | undefined {
  if (text.trim() === "") return null; // random
  if (!/^\d+$/.test(text.trim())) return undefined;
  const seed = Number(text);
  return seed < SEED_LIMIT ? seed : undefined;
}

const STATE_TITLES: Record<string, string> = {
  ready: "готова",
  busy: "занята",
  loading: "загружается",
  error: "ошибка загрузки",
  unavailable: "не запущена",
};

export function GenerationForm({
  section,
  onSubmitted,
}: {
  section: Section;
  onSubmitted: (jobId: string) => void;
}) {
  const draft = draftFor(section);
  const target = targetOf(section, draft);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!target) {
    return (
      <p class="hint">
        Задачи этой модели станут известны, когда её сервер будет запущен.
      </p>
    );
  }
  const { task, server } = target;
  const models = serversFor(section, task.task);

  const seed = parseSeed(draft.seed);
  // Inputs of this task only; one deleted since it was picked counts as missing.
  const roles = new Set(task.inputs.map((spec) => spec.role));
  const inputs = Object.fromEntries(
    Object.entries(draft.inputs).filter(
      ([role, assetId]) => roles.has(role) && !assetById(assetId)?.deleted_at,
    ),
  );
  const missingInput = task.inputs.some(
    (spec) => spec.required && !inputs[spec.role],
  );
  const missingPrompt = task.prompt === "required" && !draft.prompt.trim();
  const invalid = invalidParams(task.params_schema, draft.texts);
  const primaryInvalid = invalid.some(([, prop]) => isPrimary(prop));
  const advancedInvalid =
    invalid.some(([, prop]) => !isPrimary(prop)) || seed === undefined;
  const blocked =
    missingInput || missingPrompt || primaryInvalid || advancedInvalid;
  const hasPrimary = properties(task.params_schema).some(([, prop]) =>
    isPrimary(prop),
  );
  const changeParams = (
    params: typeof draft.params,
    texts: typeof draft.texts,
  ) => updateDraft(section, { params, texts });

  const submit = async () => {
    if (blocked || sending) return;
    setSending(true);
    setError(null);
    try {
      const job = await api.createJob({
        server: server.id,
        task: task.task,
        prompt:
          task.prompt === "none" || !draft.prompt.trim()
            ? null
            : draft.prompt.trim(),
        params: draft.params,
        count: draft.count,
        seed: seed ?? null,
        inputs,
        dependencies: {},
      });
      mergeJob(job);
      onSubmitted(job.id);
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : String(failure));
    } finally {
      setSending(false);
    }
  };

  return (
    <>
      {section.tasks.length > 1 && (
        <div class="tasks" role="group" aria-label="Задача">
          {section.tasks.map((name) => (
            <button
              key={name}
              class={name === task.task ? "active" : ""}
              aria-pressed={name === task.task}
              onClick={() => chooseTask(section, name)}
            >
              {taskLabel(name)}
            </button>
          ))}
        </div>
      )}
      <div class="field">
        <span id={`model-${section.id}`}>Модель</span>
        <div
          class="models"
          role="group"
          aria-labelledby={`model-${section.id}`}
        >
          {models.map((model) => {
            const active = model.id === server.id;
            return (
              <button
                key={model.id}
                class={active ? "active" : ""}
                aria-pressed={active}
                title={`${model.title}: ${STATE_TITLES[model.state] ?? model.state}`}
                onClick={() => chooseServer(section, model.id)}
              >
                <span class={`dot ${model.state}`} />
                {model.title}
              </button>
            );
          })}
        </div>
      </div>
      {task.inputs.map((spec) => (
        <InputPicker
          key={spec.role}
          spec={spec}
          assetId={draft.inputs[spec.role] ?? null}
          onChange={(assetId) => {
            const others = Object.entries(draft.inputs).filter(
              ([role]) => role !== spec.role,
            );
            updateDraft(section, {
              inputs: Object.fromEntries(
                assetId ? [...others, [spec.role, assetId]] : others,
              ),
            });
          }}
        />
      ))}
      {task.prompt !== "none" && (
        <label class="field">
          <span>
            Описание{task.prompt === "optional" ? " (необязательно)" : ""}
          </span>
          <textarea
            value={draft.prompt}
            placeholder="Что сгенерировать"
            onInput={(event) =>
              updateDraft(section, { prompt: event.currentTarget.value })
            }
            onKeyDown={(event) => {
              if (event.key === "Enter" && (event.ctrlKey || event.metaKey))
                void submit();
            }}
          />
        </label>
      )}
      {hasPrimary && (
        <div class="fields">
          <ParamsForm
            schema={task.params_schema}
            values={draft.params}
            texts={draft.texts}
            primary={true}
            onChange={changeParams}
          />
        </div>
      )}
      {task.max_count > 1 && (
        <div class="field">
          <span id={`count-${section.id}`}>Варианты</span>
          {task.max_count <= MAX_SEGMENTS ? (
            <div
              class="segmented"
              role="group"
              aria-labelledby={`count-${section.id}`}
            >
              {Array.from(
                { length: task.max_count },
                (_, index) => index + 1,
              ).map((count) => (
                <button
                  key={count}
                  class={count === draft.count ? "active" : ""}
                  aria-pressed={count === draft.count}
                  onClick={() => updateDraft(section, { count })}
                >
                  {count}
                </button>
              ))}
            </div>
          ) : (
            <input
              type="number"
              aria-labelledby={`count-${section.id}`}
              min={1}
              max={task.max_count}
              value={draft.count}
              onInput={(event) => {
                const count = event.currentTarget.valueAsNumber;
                if (count >= 1 && count <= task.max_count)
                  updateDraft(section, { count });
              }}
            />
          )}
        </div>
      )}
      <details
        open={draft.advanced}
        onToggle={(event) => {
          const open = event.currentTarget.open;
          if (open !== draft.advanced) updateDraft(section, { advanced: open });
        }}
      >
        <summary>Дополнительно</summary>
        <div class="fields">
          <ParamsForm
            schema={task.params_schema}
            values={draft.params}
            texts={draft.texts}
            primary={false}
            onChange={changeParams}
          />
          <label class="field">
            <span>Seed (пусто — случайный)</span>
            <input
              type="text"
              inputMode="numeric"
              class={seed === undefined ? "invalid" : ""}
              aria-invalid={seed === undefined}
              value={draft.seed}
              onInput={(event) =>
                updateDraft(section, { seed: event.currentTarget.value })
              }
            />
            {seed === undefined && (
              <span class="hint error">
                Нужно целое число от 0 до {SEED_LIMIT - 1}
              </span>
            )}
          </label>
        </div>
      </details>
      <button
        class="button primary"
        disabled={blocked || sending}
        onClick={() => void submit()}
      >
        {sending ? "Отправка…" : "Сгенерировать"}
      </button>
      {advancedInvalid && (
        <p class="hint error">Исправьте значения в «Дополнительно».</p>
      )}
      {error && (
        <p class="hint error" role="alert">
          {error}
        </p>
      )}
    </>
  );
}
