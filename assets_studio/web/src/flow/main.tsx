import { render } from "preact";
import { useEffect, useRef, useState } from "preact/hooks";
import type { Asset, Server, Task } from "../api/client";
import {
  defaults,
  invalidParams,
  ParamsForm,
  type Texts,
} from "../components/ParamsForm";
import { taskLabel } from "../format";
import {
  request,
  type FlowRecord,
  type Graph,
  type FlowNode,
  type Run,
  type TaskNode,
} from "./api";
import "./style.css";

const fresh = (): Graph => ({
  schema_version: 1,
  title: "Новый граф",
  nodes: [],
});
const statusText: Record<string, string> = {
  submitting: "Создаются задания",
  submitted: "Отправлено",
  submission_failed: "Ошибка отправки",
  queued: "В очереди",
  waiting_model: "Ожидает модель",
  running: "Выполняется",
  succeeded: "Готово",
  failed: "Ошибка",
  cancelled: "Отменено",
  cancelling: "Отмена…",
  unknown: "Нет связи",
};
const nodeName = (node: FlowNode) =>
  node.title || (node.kind === "asset" ? "Ассет" : taskLabel(node.task));
const specFor = (node: FlowNode | undefined, servers: Server[]) =>
  node?.kind === "task"
    ? servers
        .find((server) => server.id === node.server)
        ?.tasks.find((task) => task.task === node.task)
    : undefined;
const errorText = (error: unknown) =>
  error instanceof Error ? error.message : String(error);

function App() {
  const [flows, setFlows] = useState<FlowRecord[]>([]);
  const [current, setCurrent] = useState<FlowRecord | null>(null);
  const [graph, setGraph] = useState<Graph>(fresh);
  const [selected, setSelected] = useState<string | null>(null);
  const [servers, setServers] = useState<Server[]>([]);
  const [assets, setAssets] = useState<Asset[]>([]);
  const [studio, setStudio] = useState("http://127.0.0.1:9000");
  const [palette, setPalette] = useState("");
  const [search, setSearch] = useState("");
  const [assetChoice, setAssetChoice] = useState("");
  const [runs, setRuns] = useState<Run[]>([]);
  const [run, setRun] = useState<Run | null>(null);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [busy, setBusy] = useState(false);
  const [texts, setTexts] = useState<Record<string, Texts>>({});
  const pendingStart = useRef<{
    id: string;
    flow: string;
    revision: number;
  } | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);
  const dirty =
    !current || JSON.stringify(graph) !== JSON.stringify(current.graph);
  const shown = run?.graph ?? graph;
  const node = shown.nodes.find((item) => item.id === selected);
  const spec = specFor(node, servers);
  const badNumbers = graph.nodes.some((item) => {
    const task = specFor(item, servers);
    return (
      task && invalidParams(task.params_schema, texts[item.id] ?? {}).length > 0
    );
  });
  const act = (action: () => Promise<unknown>) => {
    setError("");
    void action().catch((failure: unknown) => {
      setError(errorText(failure));
    });
  };

  async function refreshLibrary() {
    const [nextServers, nextAssets] = await Promise.all([
      request<Server[]>("/studio/servers"),
      request<Asset[]>("/studio/assets?limit=1000"),
    ]);
    setServers(nextServers);
    setAssets(nextAssets);
  }
  useEffect(() => {
    act(async () => {
      const [saved, config] = await Promise.all([
        request<FlowRecord[]>("/flows"),
        request<{ studio_url: string }>("/config"),
      ]);
      setFlows(saved);
      setStudio(config.studio_url);
      const identity = new URLSearchParams(location.search).get("flow");
      const first = saved.find((item) => item.id === identity) ?? saved[0];
      if (first) {
        setCurrent(first);
        setGraph(first.graph);
      }
      await refreshLibrary();
    });
  }, []);
  useEffect(() => {
    if (!current) {
      setRuns([]);
      return;
    }
    let active = true;
    const load = async () => {
      try {
        const next = await request<Run[]>(`/flows/${current.id}/runs`);
        if (active) setRuns(next);
      } catch (failure) {
        if (active) setError(errorText(failure));
      }
    };
    void load();
    const timer = window.setInterval(() => {
      void load();
    }, 3000);
    return () => {
      active = false;
      window.clearInterval(timer);
    };
  }, [current?.id]);
  useEffect(() => {
    if (!run) return;
    let active = true;
    const identity = run.id;
    const load = async () => {
      try {
        const next = await request<Run>(`/runs/${identity}`);
        if (active) setRun(next);
      } catch (failure) {
        if (active) setError(errorText(failure));
      }
    };
    void load();
    const timer = window.setInterval(() => {
      void load();
    }, 2000);
    return () => {
      active = false;
      window.clearInterval(timer);
    };
  }, [run?.id]);
  useEffect(() => {
    const handler = (event: BeforeUnloadEvent) => {
      if (dirty && graph.nodes.length) event.preventDefault();
    };
    window.addEventListener("beforeunload", handler);
    return () => {
      window.removeEventListener("beforeunload", handler);
    };
  }, [dirty, graph.nodes.length]);

  function open(flow: FlowRecord | null) {
    if (
      dirty &&
      graph.nodes.length &&
      !window.confirm("Несохранённые изменения будут потеряны. Продолжить?")
    )
      return;
    setCurrent(flow);
    setGraph(flow?.graph ?? fresh());
    setSelected(null);
    setRun(null);
    setError("");
    setNotice("");
    setTexts({});
    history.replaceState(
      null,
      "",
      flow ? `?flow=${flow.id}` : location.pathname,
    );
  }
  async function save(): Promise<FlowRecord> {
    if (badNumbers)
      throw new Error("Исправьте числовые параметры перед сохранением");
    if (current && !dirty) return current;
    const saved = current
      ? await request<FlowRecord>(`/flows/${current.id}`, "PUT", {
          revision: current.revision,
          graph,
        })
      : await request<FlowRecord>("/flows", "POST", graph);
    setCurrent(saved);
    setFlows((previous) => [
      saved,
      ...previous.filter((item) => item.id !== saved.id),
    ]);
    setNotice("Граф сохранён");
    history.replaceState(null, "", `?flow=${saved.id}`);
    return saved;
  }
  async function start() {
    setBusy(true);
    try {
      const saved = await save();
      let pending = pendingStart.current;
      if (pending?.flow !== saved.id || pending.revision !== saved.revision) {
        pending = {
          id: crypto.randomUUID(),
          flow: saved.id,
          revision: saved.revision,
        };
        pendingStart.current = pending;
      }
      const created = await request<Run>(`/flows/${saved.id}/runs`, "POST", {
        request_id: pending.id,
      });
      pendingStart.current = null;
      setRun(created);
      setNotice("Запуск создан. Модели запускает launcher.");
      setRuns((previous) => [
        created,
        ...previous.filter((item) => item.id !== created.id),
      ]);
    } finally {
      setBusy(false);
    }
  }
  function changeNode(identity: string, change: Partial<FlowNode>) {
    setGraph((previous) => ({
      ...previous,
      nodes: previous.nodes.map((item) =>
        item.id === identity ? ({ ...item, ...change } as FlowNode) : item,
      ),
    }));
  }
  function removeNode(identity: string) {
    setGraph((previous) => ({
      ...previous,
      nodes: previous.nodes
        .filter((item) => item.id !== identity)
        .map((item) => {
          if (item.kind === "asset") return item;
          return {
            ...item,
            inputs: Object.fromEntries(
              Object.entries(item.inputs).filter(
                ([, binding]) => binding.node !== identity,
              ),
            ),
          };
        }),
    }));
    setSelected(null);
  }
  function addTask() {
    const [serverId, taskName] = palette.split("|");
    const server = servers.find((item) => item.id === serverId);
    const task = server?.tasks.find((item) => item.task === taskName);
    if (!server || !task) return;
    const identity = crypto.randomUUID();
    const item: TaskNode = {
      kind: "task",
      id: identity,
      title: "",
      server: server.id,
      task: task.task,
      params: defaults(task.params_schema),
      prompt: null,
      count: 1,
      seed: null,
      inputs: {},
      x: 120 + (graph.nodes.length % 4) * 290,
      y: 80 + Math.floor(graph.nodes.length / 4) * 230,
    };
    setGraph((previous) => ({ ...previous, nodes: [...previous.nodes, item] }));
    setSelected(identity);
  }
  function addAsset() {
    const asset = assets.find((item) => item.id === assetChoice);
    if (!assetChoice) return;
    const identity = crypto.randomUUID();
    setGraph((previous) => ({
      ...previous,
      nodes: [
        ...previous.nodes,
        {
          kind: "asset",
          id: identity,
          asset_id: assetChoice,
          title: asset?.title ?? "Ассет",
          x: 60,
          y: 80 + previous.nodes.length * 80,
        },
      ],
    }));
    setSelected(identity);
  }
  function drag(event: PointerEvent, item: FlowNode) {
    if (
      run ||
      event.button !== 0 ||
      (event.target instanceof HTMLElement && event.target.closest("button"))
    )
      return;
    setSelected(item.id);
    event.preventDefault();
    const x = event.clientX,
      y = event.clientY;
    const move = (next: PointerEvent) => {
      changeNode(item.id, {
        x: Math.max(10, item.x + next.clientX - x),
        y: Math.max(10, item.y + next.clientY - y),
      });
    };
    const end = () => {
      window.removeEventListener("pointermove", move);
      window.removeEventListener("pointerup", end);
    };
    window.addEventListener("pointermove", move);
    window.addEventListener("pointerup", end);
  }
  function layout() {
    const layers = new Map<string, number>();
    let pending = [...graph.nodes];
    for (let pass = 0; pending.length && pass <= graph.nodes.length; pass++) {
      const ready = pending.filter(
        (item) =>
          item.kind === "asset" ||
          Object.values(item.inputs).every((input) => layers.has(input.node)),
      );
      if (!ready.length) {
        setError("Для раскладки устраните цикл в связях");
        return;
      }
      for (const item of ready) {
        const sources =
          item.kind === "task"
            ? Object.values(item.inputs).map(
                (input) => layers.get(input.node) ?? 0,
              )
            : [];
        layers.set(item.id, sources.length ? Math.max(...sources) + 1 : 0);
      }
      pending = pending.filter((item) => !ready.includes(item));
    }
    const rows = new Map<number, number>();
    setGraph((previous) => ({
      ...previous,
      nodes: previous.nodes.map((item) => {
        const layer = layers.get(item.id) ?? 0,
          row = rows.get(layer) ?? 0;
        rows.set(layer, row + 1);
        return { ...item, x: 70 + layer * 300, y: 70 + row * 240 };
      }),
    }));
  }
  function exportGraph() {
    const url = URL.createObjectURL(
      new Blob([JSON.stringify(shown, null, 2)], { type: "application/json" }),
    );
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = "studio-flow.json";
    anchor.click();
    window.setTimeout(() => {
      URL.revokeObjectURL(url);
    }, 1000);
  }
  async function importGraph(file: File) {
    const candidate: unknown = JSON.parse(await file.text());
    // Validate and save through the server before putting imported data in the editor.
    const saved = await request<FlowRecord>("/flows", "POST", candidate);
    setFlows((previous) => [saved, ...previous]);
    setCurrent(saved);
    setGraph(saved.graph);
    setRun(null);
    setSelected(null);
    setTexts({});
    setNotice("Граф импортирован");
  }
  function connect(role: string, source: string) {
    if (node?.kind !== "task") return;
    const inputs = { ...node.inputs };
    if (source) inputs[role] = { node: source, output_index: 0 };
    else {
      changeNode(node.id, {
        inputs: Object.fromEntries(
          Object.entries(inputs).filter(([name]) => name !== role),
        ),
      });
      return;
    }
    changeNode(node.id, { inputs });
  }
  const matchingAssets = assets.filter((asset) =>
    `${asset.title ?? ""} ${asset.id} ${asset.tags.join(" ")}`
      .toLowerCase()
      .includes(search.toLowerCase()),
  );
  const sourceOptions = (task: Task, role: string) =>
    shown.nodes.filter((item) => {
      if (item.id === node?.id) return false;
      const accepted =
        task.inputs.find((input) => input.role === role)?.mime ?? [];
      const produced =
        item.kind === "asset"
          ? [assets.find((asset) => asset.id === item.asset_id)?.mime ?? ""]
          : (specFor(item, servers)?.output_mime ?? []);
      return produced.some((mime) =>
        accepted.some(
          (pattern) =>
            pattern === mime || pattern === `${mime.split("/")[0] ?? ""}/*`,
        ),
      );
    });

  return (
    <div class="flow-shell">
      <header class="flow-header">
        <div class="flow-brand">
          <span class="flow-symbol">◇</span>
          <strong>Studio flow</strong>
        </div>
        <div class="flow-title">
          <input
            aria-label="Название графа"
            value={graph.title}
            disabled={!!run}
            onInput={(event) => {
              setGraph((previous) => ({
                ...previous,
                title: event.currentTarget.value,
              }));
            }}
          />
          <span>
            {current ? `версия ${current.revision}` : "новый граф"}
            {dirty ? " · не сохранён" : ""}
          </span>
        </div>
        <a href={studio} target="_blank" rel="noreferrer">
          Открыть студию ↗
        </a>
        <button
          disabled={busy || badNumbers || !!run}
          onClick={() => {
            act(save);
          }}
        >
          Сохранить
        </button>
        <button
          class="primary"
          disabled={
            busy ||
            badNumbers ||
            !graph.nodes.some((item) => item.kind === "task") ||
            !!run
          }
          onClick={() => {
            act(start);
          }}
        >
          {busy ? "Отправка…" : "▶ Запустить"}
        </button>
      </header>
      {error && (
        <div class="flow-alert error" role="alert">
          {error}
          <button
            onClick={() => {
              setError("");
            }}
          >
            ×
          </button>
        </div>
      )}
      {notice && !error && <div class="flow-alert">{notice}</div>}
      <div class="flow-workspace">
        <aside class="flow-sidebar">
          <div class="section-heading">
            Сохранённые графы{" "}
            <button
              aria-label="Новый граф"
              title="Новый граф"
              onClick={() => {
                open(null);
              }}
            >
              +
            </button>
          </div>
          <div class="flow-list">
            {flows.map((flow) => (
              <button
                key={flow.id}
                class={current?.id === flow.id ? "active" : ""}
                onClick={() => {
                  open(flow);
                }}
              >
                <span>{flow.graph.title}</span>
                <small>{flow.graph.nodes.length} узлов</small>
              </button>
            ))}
          </div>
          <div class="flow-file-actions">
            <button onClick={exportGraph}>Экспорт JSON</button>
            <button
              onClick={() => {
                fileInput.current?.click();
              }}
            >
              Импорт
            </button>
            <input
              type="file"
              accept="application/json,.json"
              hidden
              ref={fileInput}
              onChange={(event) => {
                const file = event.currentTarget.files?.[0];
                if (file)
                  act(async () => {
                    await importGraph(file);
                  });
                event.currentTarget.value = "";
              }}
            />
          </div>
          <div class="section-heading">Добавить шаг</div>
          <label>
            Модель и задача
            <select
              aria-label="Модель и задача"
              value={palette}
              disabled={!!run}
              onChange={(event) => {
                setPalette(event.currentTarget.value);
              }}
            >
              <option value="">Выберите задачу</option>
              {servers.flatMap((server) =>
                server.tasks.map((task) => (
                  <option
                    key={`${server.id}|${task.task}`}
                    value={`${server.id}|${task.task}`}
                  >
                    {server.title} · {taskLabel(task.task)}
                  </option>
                )),
              )}
            </select>
          </label>
          <button disabled={!palette || !!run} onClick={addTask}>
            + Добавить задачу
          </button>
          <div class="section-heading">Вход из библиотеки</div>
          <input
            aria-label="Поиск ассета"
            placeholder="Название, тег или ID"
            value={search}
            onInput={(event) => {
              setSearch(event.currentTarget.value);
            }}
          />
          <select
            aria-label="Ассет из библиотеки"
            value={assetChoice}
            disabled={!!run}
            onChange={(event) => {
              setAssetChoice(event.currentTarget.value);
            }}
          >
            <option value="">Выберите ассет</option>
            {matchingAssets.map((asset) => (
              <option key={asset.id} value={asset.id}>
                {asset.title ?? asset.id} · {asset.mime}
              </option>
            ))}
          </select>
          <button disabled={!assetChoice || !!run} onClick={addAsset}>
            + Добавить ассет
          </button>
          <button
            class="quiet"
            onClick={() => {
              act(refreshLibrary);
            }}
          >
            Обновить модели и библиотеку
          </button>
          <p class="flow-hint">
            Соедините выходы с входами в панели узла. Модель может быть
            выключена: её запустит launcher.
          </p>
        </aside>
        <main class="flow-center">
          <div class="canvas-toolbar">
            <span>
              {run
                ? `Запуск · версия ${run.revision} · ${statusText[run.status ?? run.state] ?? run.state}`
                : "Граф заданий"}
            </span>
            {run ? (
              <>
                <button
                  onClick={() => {
                    setRun(null);
                  }}
                >
                  К редактированию
                </button>
                {(run.state === "submitting" ||
                  run.state === "cancelling" ||
                  Object.values(run.node_jobs ?? {}).some(
                    (job) =>
                      !["succeeded", "failed", "cancelled"].includes(
                        job.status,
                      ),
                  )) && (
                  <button
                    onClick={() => {
                      act(async () => {
                        setRun(
                          await request<Run>(`/runs/${run.id}/cancel`, "POST"),
                        );
                      });
                    }}
                  >
                    Отменить запуск
                  </button>
                )}
              </>
            ) : (
              <button onClick={layout}>Разложить граф</button>
            )}
          </div>
          {run?.error && <div class="flow-alert error">{run.error}</div>}
          <div class="canvas-scroll">
            <div
              class="graph-canvas"
              style={{
                width: Math.max(
                  1400,
                  ...shown.nodes.map((item) => item.x + 330),
                ),
                height: Math.max(
                  900,
                  ...shown.nodes.map((item) => item.y + 300),
                ),
              }}
            >
              <svg class="graph-links" width="100%" height="100%">
                <defs>
                  <marker
                    id="arrow"
                    viewBox="0 0 10 10"
                    refX="9"
                    refY="5"
                    markerWidth="6"
                    markerHeight="6"
                    orient="auto"
                  >
                    <path d="M 0 0 L 10 5 L 0 10 z" />
                  </marker>
                </defs>
                {shown.nodes.flatMap((target) =>
                  target.kind === "asset"
                    ? []
                    : Object.entries(target.inputs).map(([role, binding]) => {
                        const source = shown.nodes.find(
                          (item) => item.id === binding.node,
                        );
                        if (!source) return null;
                        const index =
                          specFor(target, servers)?.inputs.findIndex(
                            (input) => input.role === role,
                          ) ?? 0;
                        const x1 = source.x + 238,
                          y1 = source.y + 60,
                          x2 = target.x,
                          y2 = target.y + 86 + Math.max(0, index) * 22;
                        const bend = Math.max(65, Math.abs(x2 - x1) * 0.4);
                        return (
                          <path
                            key={`${target.id}-${role}`}
                            d={`M${x1},${y1} C${x1 + bend},${y1} ${x2 - bend},${y2} ${x2},${y2}`}
                            marker-end="url(#arrow)"
                          />
                        );
                      }),
                )}
              </svg>
              {!shown.nodes.length && (
                <div class="flow-empty">
                  <div>◇ → ◇ → ◇</div>
                  <h1>Один граф — вся цепочка</h1>
                  <p>
                    Добавьте вход из библиотеки и задачи слева.
                    <br />
                    Соедините их, сохраните и запускайте снова.
                  </p>
                </div>
              )}
              {shown.nodes.map((item) => {
                const task = specFor(item, servers),
                  job = run?.node_jobs?.[item.id];
                return (
                  <div
                    key={item.id}
                    class={`graph-node ${item.kind} ${selected === item.id ? "selected" : ""} ${job?.status ?? ""}`}
                    tabIndex={0}
                    role="group"
                    aria-label={nodeName(item)}
                    onKeyDown={(event) => {
                      if (event.key === "Enter") setSelected(item.id);
                    }}
                    style={{ left: item.x, top: item.y }}
                    onClick={() => {
                      setSelected(item.id);
                    }}
                  >
                    <div
                      class="node-header"
                      onPointerDown={(event) => {
                        drag(event, item);
                      }}
                    >
                      <span>{item.kind === "asset" ? "ВХОД" : "ЗАДАЧА"}</span>
                      {!run && (
                        <button
                          aria-label={`Удалить ${nodeName(item)}`}
                          onClick={(event) => {
                            event.stopPropagation();
                            removeNode(item.id);
                          }}
                        >
                          ×
                        </button>
                      )}
                    </div>
                    <strong>{nodeName(item)}</strong>
                    <div class="node-subtitle">
                      {item.kind === "asset"
                        ? (assets.find((asset) => asset.id === item.asset_id)
                            ?.mime ?? item.asset_id)
                        : (servers.find((server) => server.id === item.server)
                            ?.title ?? item.server)}
                    </div>
                    {task?.inputs.map((input) => (
                      <div
                        class={`node-port ${item.kind === "task" && item.inputs[input.role] ? "connected" : ""}`}
                        key={input.role}
                      >
                        ● {input.role}
                        {input.required ? "" : " (необяз.)"}
                      </div>
                    ))}
                    {job && (
                      <div class={`node-status ${job.status}`}>
                        {statusText[job.status] ?? job.status}
                      </div>
                    )}
                    <span class="node-output">●</span>
                  </div>
                );
              })}
            </div>
          </div>
          <div class="runs-strip">
            <strong>Запуски</strong>
            {!runs.length && (
              <span class="flow-hint">
                Здесь появится история сохранённых запусков.
              </span>
            )}
            {runs.map((item) => (
              <button
                key={item.id}
                class={run?.id === item.id ? "active" : ""}
                onClick={() => {
                  act(async () => {
                    setRun(await request<Run>(`/runs/${item.id}`));
                    setSelected(null);
                  });
                }}
              >
                {new Date(item.created_at).toLocaleString()}{" "}
                <small>v{item.revision}</small>
              </button>
            ))}
          </div>
        </main>
        <aside class="flow-inspector">
          <div class="section-heading">
            {node ? "Настройка узла" : "Свойства"}
          </div>
          {!node ? (
            <p class="flow-hint">
              Выберите узел. Перетаскивайте карточки за верхнюю полосу. Связи
              задаются по ролям входов, поэтому один выход можно использовать в
              нескольких задачах.
            </p>
          ) : (
            <>
              <fieldset disabled={!!run}>
                <label>
                  Название
                  <input
                    value={node.title}
                    placeholder={nodeName(node)}
                    onInput={(event) => {
                      changeNode(node.id, { title: event.currentTarget.value });
                    }}
                  />
                </label>
                {node.kind === "asset" ? (
                  <>
                    <label>
                      ID ассета
                      <input
                        value={node.asset_id}
                        onInput={(event) => {
                          changeNode(node.id, {
                            asset_id: event.currentTarget.value,
                          });
                        }}
                      />
                    </label>
                    <a href={`/flow-api/studio/assets/${node.asset_id}/file`}>
                      Скачать вход
                    </a>
                  </>
                ) : (
                  <>
                    <p class="flow-hint">
                      {taskLabel(node.task)} · {node.server}
                    </p>
                    {!spec && (
                      <p class="flow-alert error">
                        Запустите сервер один раз и обновите список моделей.
                      </p>
                    )}
                    {spec?.inputs.map((input) => (
                      <label key={input.role}>
                        {input.labels?.ru ?? input.description ?? input.role}
                        <select
                          aria-label={`Вход ${input.role}`}
                          value={node.inputs[input.role]?.node ?? ""}
                          onChange={(event) => {
                            connect(input.role, event.currentTarget.value);
                          }}
                        >
                          <option value="">
                            {input.required
                              ? "Выберите источник"
                              : "Не использовать"}
                          </option>
                          {sourceOptions(spec, input.role).map((source) => (
                            <option key={source.id} value={source.id}>
                              {nodeName(source)}
                            </option>
                          ))}
                        </select>
                        {node.inputs[input.role] && (
                          <span class="flow-hint">
                            Выход №{" "}
                            <input
                              class="output-number"
                              type="number"
                              min="0"
                              value={node.inputs[input.role]?.output_index ?? 0}
                              onInput={(event) => {
                                const binding = node.inputs[input.role];
                                const index = Number(event.currentTarget.value);
                                if (
                                  binding &&
                                  Number.isInteger(index) &&
                                  index >= 0
                                )
                                  changeNode(node.id, {
                                    inputs: {
                                      ...node.inputs,
                                      [input.role]: {
                                        ...binding,
                                        output_index: index,
                                      },
                                    },
                                  });
                              }}
                            />
                          </span>
                        )}
                      </label>
                    ))}
                    {spec && spec.prompt !== "none" && (
                      <label>
                        Промпт
                        <textarea
                          rows={5}
                          value={node.prompt ?? ""}
                          onInput={(event) => {
                            changeNode(node.id, {
                              prompt: event.currentTarget.value,
                            });
                          }}
                        />
                      </label>
                    )}
                    {spec && (
                      <>
                        {[true, false].map((primary) => (
                          <ParamsForm
                            key={String(primary)}
                            schema={spec.params_schema}
                            values={node.params}
                            texts={texts[node.id] ?? {}}
                            primary={primary}
                            onChange={(values, nextTexts) => {
                              changeNode(node.id, { params: values });
                              setTexts((previous) => ({
                                ...previous,
                                [node.id]: nextTexts,
                              }));
                            }}
                          />
                        ))}
                        <label>
                          Вариантов
                          <input
                            type="number"
                            min="1"
                            max={spec.max_count}
                            value={node.count}
                            onInput={(event) => {
                              const count = Number(event.currentTarget.value);
                              if (Number.isInteger(count) && count > 0)
                                changeNode(node.id, { count });
                            }}
                          />
                        </label>
                      </>
                    )}
                    <label>
                      Seed (пусто — случайный)
                      <input
                        type="number"
                        min="0"
                        max="4294967295"
                        value={node.seed ?? ""}
                        onInput={(event) => {
                          const value = event.currentTarget.value;
                          const seed = Number(value);
                          if (!value || Number.isInteger(seed))
                            changeNode(node.id, { seed: value ? seed : null });
                        }}
                      />
                    </label>
                  </>
                )}
              </fieldset>
              {run && (
                <div class="node-results">
                  <strong>Результат узла</strong>
                  {run.node_jobs?.[node.id]?.error_message && (
                    <p class="flow-alert error">
                      {run.node_jobs[node.id]?.error_message}
                    </p>
                  )}
                  {run.node_jobs?.[node.id]?.outputs.map((asset, index) => (
                    <a
                      key={asset}
                      href={`/flow-api/studio/assets/${asset}/file`}
                    >
                      Скачать результат {index + 1} ↗
                    </a>
                  ))}
                  {run.node_jobs?.[node.id] && (
                    <small>Задание {run.node_jobs[node.id]?.id}</small>
                  )}
                </div>
              )}
            </>
          )}
        </aside>
      </div>
    </div>
  );
}

const root = document.getElementById("flow-app");
if (root) render(<App />, root);
