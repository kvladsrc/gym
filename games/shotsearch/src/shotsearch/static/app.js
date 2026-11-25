"use strict";

// Query items: {kind: "upload" | "image" | "shot", value, label}
const items = [];
const REVIEWS = [
  "ни одного",
  "1–2",
  "3–9",
  "10–29",
  "30–99",
  "100–299",
  "300–999",
  "1000–2999",
  "3000–9999",
  "10 000 и больше",
];
const $ = (id) => document.getElementById(id);
const FIELDS = [
  "sort",
  "pool",
  "limit",
  "year_from",
  "year_to",
  "tags",
  "min_reviews",
];

function el(tag, props = {}, ...children) {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(props)) {
    if (key === "class") node.className = value;
    else if (key.startsWith("on")) node.addEventListener(key.slice(2), value);
    else node[key] = value;
  }
  for (const child of children) {
    if (child !== null && child !== undefined) node.append(child);
  }
  return node;
}

function status(text) {
  $("status").textContent = text;
}

function thumb(item) {
  if (item.kind === "upload") return `/upload/${item.value}`;
  if (item.kind === "shot") return `/shot/${item.value}`;
  return item.value;
}

function drawItems() {
  const box = $("items");
  box.replaceChildren(
    ...items.map((item, i) =>
      el(
        "div",
        { class: "item" },
        el("img", { src: thumb(item), alt: item.label }),
        el("span", { textContent: item.label, title: item.label }),
        el("button", {
          type: "button",
          textContent: "×",
          title: "Убрать",
          onclick: () => {
            items.splice(i, 1);
            drawItems();
            search();
          },
        }),
      ),
    ),
  );
}

async function upload(file) {
  status(`Загружаю ${file.name || "картинку"}…`);
  const response = await fetch("/api/upload", { method: "POST", body: file });
  const answer = await response.json();
  if (!response.ok) throw new Error(answer.error);
  items.push({
    kind: "upload",
    value: answer.id,
    label: file.name || answer.id,
  });
  drawItems();
}

async function addFiles(files) {
  try {
    for (const file of files) {
      if (file.type.startsWith("image/")) await upload(file);
    }
    search();
  } catch (error) {
    status(`Ошибка: ${error.message}`);
  }
}

function params() {
  const p = new URLSearchParams();
  for (const item of items) p.append(item.kind, item.value);
  const text = $("text").value.trim();
  if (text) p.set("text", text);
  p.set("ranking", document.querySelector("input[name=ranking]:checked").value);
  for (const id of FIELDS) {
    const value = $(id).value.trim();
    if (value) p.set(id, value);
  }
  if ($("unseen_only").checked) p.set("unseen_only", "1");
  if ($("all_shots").checked) p.set("per_game", "0");
  return p;
}

function restore(p) {
  items.length = 0;
  for (const kind of ["upload", "image", "shot"]) {
    for (const value of p.getAll(kind))
      items.push({ kind, value, label: value });
  }
  $("text").value = p.get("text") || "";
  const ranking = p.get("ranking") || "both";
  for (const radio of document.querySelectorAll("input[name=ranking]")) {
    radio.checked = radio.value === ranking;
  }
  for (const id of FIELDS) if (p.has(id)) $(id).value = p.get(id);
  $("unseen_only").checked = p.get("unseen_only") === "1";
  $("all_shots").checked = p.get("per_game") === "0";
  drawItems();
}

function bar(space, label, value) {
  const fill = el("div", { class: "fill" });
  fill.style.width = `${Math.max(0, Math.min(1.5, value)) * (100 / 1.5)}%`;
  return el(
    "div",
    { class: `bar ${space}` },
    el("span", { textContent: label }),
    el("div", { class: "track" }, fill),
    el("span", { class: "num", textContent: value.toFixed(2) }),
  );
}

function card(result) {
  const game = result.game;
  const forecast = result.forecast;
  const real =
    game.reviews === null
      ? "отзывов нет данных"
      : `отзывов ${game.reviews.toLocaleString("ru")}` +
        (game.positive === null ? "" : `, ${game.positive}% положительных`);
  const guess = forecast
    ? `прогноз Qwen: ${REVIEWS[Math.max(0, Math.min(9, Math.round(forecast.review_class)))]} ` +
      `(${forecast.review_class.toFixed(1)})`
    : "прогноза нет";
  const bars = [];
  if ("look" in result.closeness)
    bars.push(bar("look", "вид", result.closeness.look));
  if ("meaning" in result.closeness)
    bars.push(bar("meaning", "смысл", result.closeness.meaning));
  if (!bars.length && "meaning" in result.similarity) {
    bars.push(
      el("div", {
        class: "meta",
        textContent: `сходство с текстом ${result.similarity.meaning}`,
      }),
    );
  }
  return el(
    "div",
    { class: "card" },
    el("img", {
      src: `/shot/${result.shot}`,
      alt: game.name,
      loading: "lazy",
      onclick: () => zoom(`/shot/${result.shot}`),
    }),
    el(
      "div",
      { class: "body" },
      el("a", {
        href: result.steam_url,
        target: "_blank",
        rel: "noopener noreferrer",
        textContent: `${game.name} (${game.year ?? "?"})`,
      }),
      el("div", { class: "meta", textContent: real }),
      el(
        "div",
        { class: "meta" },
        guess,
        forecast && forecast.game_seen_in_training
          ? el("span", { class: "seen", textContent: " · модель видела" })
          : null,
      ),
      el("div", {
        class: "meta",
        textContent: game.tags.slice(0, 6).join(", "),
      }),
      el("div", { class: "bars" }, ...bars),
      el(
        "div",
        { class: "actions" },
        el("button", {
          type: "button",
          textContent: "Похожие на этот",
          onclick: () => {
            items.length = 0;
            items.push({ kind: "shot", value: result.shot, label: game.name });
            drawItems();
            search();
          },
        }),
        el("button", {
          type: "button",
          textContent: "+ в запрос",
          onclick: () => {
            items.push({ kind: "shot", value: result.shot, label: game.name });
            drawItems();
            search();
          },
        }),
      ),
    ),
  );
}

function zoom(src) {
  const overlay = el(
    "div",
    { id: "zoom", onclick: () => overlay.remove() },
    el("img", { src }),
  );
  document.body.append(overlay);
}

let generation = 0;
async function search() {
  const p = params();
  if (!items.length && !p.get("text")) {
    status("Добавьте картинку, кадр или описание.");
    $("results").replaceChildren();
    return;
  }
  if (items.length && p.get("text")) {
    status(
      "Описание ищется отдельно от картинок: уберите картинки или очистите описание.",
    );
    return;
  }
  history.replaceState(null, "", `#${p}`);
  const mine = ++generation;
  status("Ищу… (первый запрос загружает модели, до минуты)");
  const started = performance.now();
  try {
    const response = await fetch(`/api/search?${p}`);
    const answer = await response.json();
    if (mine !== generation) return;
    if (!response.ok) throw new Error(answer.error);
    $("results").replaceChildren(...answer.results.map(card));
    const seconds = ((performance.now() - started) / 1000).toFixed(1);
    status(`${answer.results.length} результатов за ${seconds} с`);
  } catch (error) {
    if (mine === generation) status(`Ошибка: ${error.message}`);
  }
}

async function init() {
  try {
    const info = await (await fetch("/api/info")).json();
    $("info").textContent =
      `${info.shots.toLocaleString("ru")} скриншотов, ` +
      `${info.games.toLocaleString("ru")} игр`;
  } catch {
    $("info").textContent = "";
  }
  $("file").addEventListener("change", (event) =>
    addFiles([...event.target.files]),
  );
  const drop = $("drop");
  drop.addEventListener("dragover", (event) => {
    event.preventDefault();
    drop.classList.add("over");
  });
  drop.addEventListener("dragleave", () => drop.classList.remove("over"));
  drop.addEventListener("drop", (event) => {
    event.preventDefault();
    drop.classList.remove("over");
    addFiles([...event.dataTransfer.files]);
  });
  document.addEventListener("paste", (event) => {
    const files = [...event.clipboardData.files];
    if (files.length) addFiles(files);
  });
  $("add-url").addEventListener("click", async () => {
    const url = $("url").value.trim();
    if (!url) return;
    status("Скачиваю картинку…");
    try {
      const response = await fetch(
        `/api/fetch?${new URLSearchParams({ url })}`,
        {
          method: "POST",
        },
      );
      const answer = await response.json();
      if (!response.ok) throw new Error(answer.error);
      items.push({ kind: "upload", value: answer.id, label: url });
      $("url").value = "";
      drawItems();
      search();
    } catch (error) {
      status(`Ошибка: ${error.message}`);
    }
  });
  $("go").addEventListener("click", search);
  $("text").addEventListener("keydown", (event) => {
    if (event.key === "Enter") search();
  });
  for (const input of document.querySelectorAll(
    ".controls input, .controls select",
  )) {
    input.addEventListener("change", search);
  }
  if (location.hash.length > 1) {
    restore(new URLSearchParams(location.hash.slice(1)));
    search();
  }
}

init();
