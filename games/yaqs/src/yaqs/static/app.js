"use strict";
// Book text is untrusted: every string goes into the page as a text node.

// Interface strings. A language is used when the page URL has ?lang=CODE or the browser
// prefers it; English fills any key a translation lacks. Plural forms are keyed by
// Intl.PluralRules categories (one, few, many, other).
const STRINGS = {
  en: {
    books: "Books",
    placeholder: "Search the library ( / )",
    query: "Query",
    mode: "Search mode",
    semantic: "by meaning",
    hybrid: "hybrid",
    lexical: "by words",
    book: "Book",
    allBooks: "all books",
    limit: "Number of results",
    find: "Search",
    intro: "Search the library: “by meaning” understands paraphrases and queries in another "
      + "language than the book; “by words” finds exact terms.",
    searching: "Searching…",
    found: (hits, time) => `${plural(hits, { one: "passage", other: "passages" })} in ${time} s`,
    omitted: (blocks) => ` · ${blocks} more did not fit the budget`,
    unpaged: "unpaged text",
    pageNumber: (page) => `p. ${page}`,
    printedLabel: (label) => ` (printed ${label})`,
    wholePassage: "Whole passage",
    collapse: "Collapse",
    context: "Context",
    page: "Page",
    copyReference: "Copy a reference to the source",
    cite: "Cite",
    previous: "Previous (←)",
    next: "Next (→)",
    of: (pages) => `of ${pages}`,
    searchBook: "Search this book",
    readOn: "Continue the page",
    neighbors: "Passage with its neighbors in reading order",
    readFromStart: "Read from the first page",
    indexStatus: (status) => `index: ${status}`,
    filter: "Filter",
    pagesColumn: "Pages",
    chunksColumn: "Passages",
    stats: (counts) => [
      plural(counts.books, { one: "book", other: "books" }),
      plural(counts.pages, { one: "page", other: "pages" }),
      plural(counts.chunks, { one: "passage", other: "passages" }),
    ].join(" · "),
  },
  ru: {
    books: "Книги",
    placeholder: "Что найти в библиотеке? ( / )",
    query: "Запрос",
    mode: "Режим поиска",
    semantic: "по смыслу",
    hybrid: "гибрид",
    lexical: "по словам",
    book: "Книга",
    allBooks: "все книги",
    limit: "Сколько результатов",
    find: "Найти",
    intro: "Поиск по библиотеке: «по смыслу» понимает перефразировки и запрос на другом языке, "
      + "чем книга, «по словам» ищет точные термины.",
    searching: "Ищу…",
    found: (hits, time) =>
      `${plural(hits, { one: "фрагмент", few: "фрагмента", other: "фрагментов" })} за ${time} с`,
    omitted: (blocks) => ` · ещё ${blocks} не вошло в бюджет`,
    unpaged: "текст без страниц",
    pageNumber: (page) => `стр. ${page}`,
    printedLabel: (label) => ` (в книге ${label})`,
    wholePassage: "Весь фрагмент",
    collapse: "Свернуть",
    context: "Контекст",
    page: "Страница",
    copyReference: "Скопировать ссылку на источник",
    cite: "Цитата",
    previous: "Предыдущая (←)",
    next: "Следующая (→)",
    of: (pages) => `из ${pages}`,
    searchBook: "Искать в этой книге",
    readOn: "Дальше по странице",
    neighbors: "Фрагмент с соседями в порядке чтения",
    readFromStart: "Читать с первой страницы",
    indexStatus: (status) => `индекс: ${status}`,
    filter: "Фильтр",
    pagesColumn: "Стр.",
    chunksColumn: "Фрагм.",
    stats: (counts) => [
      plural(counts.books, { one: "книга", few: "книги", other: "книг" }),
      `${number(counts.pages)} стр.`,
      plural(counts.chunks, { one: "фрагмент", few: "фрагмента", other: "фрагментов" }),
    ].join(" · "),
  },
};

// Static elements name their string in data-i18n (text) or data-i18n-ATTRIBUTE.
const TRANSLATED_ATTRIBUTES = ["title", "aria-label", "placeholder"];

function language() {
  const asked = new URLSearchParams(location.search).get("lang");
  const preferred = [asked, ...(navigator.languages || [navigator.language])];
  return preferred.map((tag) => String(tag || "").slice(0, 2).toLowerCase())
    .find((code) => Object.hasOwn(STRINGS, code)) || "en";
}

const LANG = language();
const pluralRules = new Intl.PluralRules(LANG);
const numbers = new Intl.NumberFormat(LANG);
const seconds = new Intl.NumberFormat(LANG, { minimumFractionDigits: 1, maximumFractionDigits: 1 });

function t(key, ...args) {
  const value = STRINGS[LANG][key] ?? STRINGS.en[key];
  return typeof value === "function" ? value(...args) : value;
}

function number(value) {
  return numbers.format(value);
}

function plural(value, forms) {
  return `${number(value)} ${forms[pluralRules.select(value)] ?? forms.other}`;
}

function localize() {
  document.documentElement.lang = LANG;
  for (const node of document.querySelectorAll("[data-i18n]")) node.textContent = t(node.dataset.i18n);
  for (const attribute of TRANSLATED_ATTRIBUTES) {
    for (const node of document.querySelectorAll(`[data-i18n-${attribute}]`)) {
      node.setAttribute(attribute, t(node.getAttribute(`data-i18n-${attribute}`)));
    }
  }
}

const books = new Map();
let shownSearch = "";
let shownReader = "";
let words = [];

function el(tag, attrs = {}, ...children) {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(attrs)) {
    if (value == null || value === false) continue;
    if (key === "class") node.className = value;
    else if (key.startsWith("on")) node.addEventListener(key.slice(2), value);
    else node.setAttribute(key, value === true ? "" : value);
  }
  for (const child of children.flat()) {
    if (child != null && child !== false) node.append(child instanceof Node ? child : String(child));
  }
  return node;
}

async function api(path) {
  const response = await fetch(path, { headers: { Accept: "application/json" } });
  const body = await response.json().catch(() => ({ error: response.statusText }));
  if (!response.ok) throw new Error(body.error || response.statusText);
  return body;
}

function hashState() {
  return new URLSearchParams(location.hash.slice(1));
}

function go(changes) {
  const state = hashState();
  for (const [key, value] of Object.entries(changes)) {
    if (value == null || value === "") state.delete(key);
    else state.set(key, value);
  }
  location.hash = state.toString();
}

function terms(query) {
  return [...new Set(query.toLowerCase().match(/[\p{L}\p{N}]{3,}/gu) || [])];
}

function highlighted(text) {
  if (!words.length) return [text];
  const escaped = words.map((word) => word.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"));
  return text
    .split(new RegExp(`(${escaped.join("|")})`, "giu"))
    .map((part, index) => (index % 2 ? el("mark", {}, part) : part));
}

function pageName(source) {
  if (!source.page_number) return t("unpaged");
  const label = source.page_label && source.page_label !== String(source.page_number);
  return t("pageNumber", source.page_number) + (label ? t("printedLabel", source.page_label) : "");
}

function sourceLine(source, extra = []) {
  return el(
    "div",
    { class: "meta" },
    [pageName(source), source.chapter, ...extra].filter(Boolean).join(" · "),
  );
}

function bookLine(source) {
  return el(
    "div",
    { class: "title" },
    source.title,
    source.author ? el("span", { class: "author" }, ` — ${source.author}`) : null,
  );
}

function readPage(source, start = 0) {
  go({ read: `page:${source.book_id}:${source.page_number}:${start}` });
}

function hitCard(hit) {
  const text = el("div", { class: "text clamped" }, highlighted(hit.text));
  const more = el("button", {
    type: "button",
    onclick: () => {
      text.classList.toggle("clamped");
      more.textContent = t(text.classList.contains("clamped") ? "wholePassage" : "collapse");
    },
  }, t("wholePassage"));
  const score = hit.score != null ? el("span", { class: "score" }, `score ${hit.score}`) : null;
  return el(
    "article",
    { class: "hit", "data-id": hit.id },
    bookLine(hit),
    el("div", { class: "meta" }, pageName(hit), hit.chapter ? ` · ${hit.chapter}` : "",
      score ? " · " : "", score),
    text,
    el(
      "div",
      { class: "actions" },
      more,
      el("button", { type: "button", onclick: () => go({ read: `passage:${hit.id}` }) }, t("context")),
      el("button", { type: "button", onclick: () => readPage(hit, hit.start_char || 0) }, t("page")),
      el("button", {
        type: "button",
        title: t("copyReference"),
        onclick: () => navigator.clipboard?.writeText(
          `${hit.title}, ${pageName(hit)}${hit.chapter ? `, ${hit.chapter}` : ""} [chunk ${hit.id}]`,
        ),
      }, t("cite")),
    ),
  );
}

async function runSearch(state) {
  const results = document.getElementById("results");
  const query = state.get("q") || "";
  words = terms(query);
  if (!query) {
    results.replaceChildren(el("p", { class: "status" }, t("intro")));
    return;
  }
  results.replaceChildren(el("p", { class: "status" }, t("searching")));
  const params = new URLSearchParams({ q: query, mode: state.get("mode") || "semantic", limit: state.get("limit") || "10" });
  if (state.get("book")) params.set("book", state.get("book"));
  try {
    const started = performance.now();
    const result = await api(`/api/search?${params}`);
    const elapsed = seconds.format((performance.now() - started) / 1000);
    const omitted = result.context.omitted_blocks;
    results.replaceChildren(
      el("p", { class: "status" }, t("found", result.hits.length, elapsed),
        omitted ? t("omitted", omitted) : ""),
      ...result.hits.map(hitCard),
    );
  } catch (error) {
    results.replaceChildren(el("p", { class: "status error" }, error.message));
  }
}

function pageNav(page, count) {
  const number = el("input", {
    type: "number", min: page.page_number ? 1 : 0, max: count || null,
    value: page.page_number, "aria-label": t("page"),
  });
  const open = (value) => readPage({ book_id: page.book_id, page_number: value });
  number.addEventListener("change", () => open(Number(number.value)));
  const paginated = page.page_number > 0;
  return el(
    "div",
    { class: "nav" },
    el("button", { type: "button", disabled: !paginated || page.page_number <= 1,
      onclick: () => open(page.page_number - 1), title: t("previous") }, "←"),
    number,
    count ? el("span", { class: "meta" }, t("of", count)) : null,
    el("button", { type: "button", disabled: !paginated || page.page_number >= count,
      onclick: () => open(page.page_number + 1), title: t("next") }, "→"),
    el("button", { type: "button", onclick: () => go({ book: page.book_id }) }, t("searchBook")),
  );
}

async function showPage(reader, bookId, number, start) {
  const page = await api(`/api/page/${bookId}/${number}?start=${start}`);
  const count = books.get(bookId)?.page_count;
  const windows = el("div", {}, el("div", { class: "window text" }, highlighted(page.text)));
  const body = [bookLine(page), sourceLine(page), pageNav(page, count), windows];
  let next = page.next_start_char;
  const more = el("button", { type: "button" }, t("readOn"));
  more.addEventListener("click", async () => {
    const window = await api(`/api/page/${bookId}/${number}?start=${next}`);
    windows.append(el("div", { class: "window text" }, highlighted(window.text)));
    next = window.next_start_char;
    if (window.complete || next == null) more.remove();
  });
  if (!page.complete && next != null) body.push(el("div", { class: "actions" }, more));
  reader.replaceChildren(...body);
}

async function showPassage(reader, chunkId) {
  const passage = await api(`/api/passage/${chunkId}?neighbors=2`);
  reader.replaceChildren(
    el("p", { class: "status" }, t("neighbors")),
    ...passage.hits.map((block) => el(
      "div",
      { class: block.chunk_ids.includes(chunkId) ? "block target" : "block" },
      bookLine(block),
      sourceLine(block),
      el("div", { class: "text" }, highlighted(block.text)),
      el("div", { class: "actions" },
        el("button", { type: "button", onclick: () => readPage(block, block.start_char || 0) }, t("page"))),
    )),
  );
}

function showBooks(reader) {
  const rows = [...books.values()].map((book) => el(
    "tr",
    {},
    el("td", { class: "num" }, book.id),
    el("td", {},
      el("button", { type: "button", class: "link", title: t("readFromStart"),
        onclick: () => readPage({ book_id: book.id, page_number: book.page_count ? 1 : 0 }) }, book.title),
      book.author ? el("div", { class: "meta" }, book.author) : null,
      book.status ? el("div", { class: "meta error" }, t("indexStatus", book.status)) : null),
    el("td", { class: "num" }, book.page_count),
    el("td", { class: "num" }, book.chunks),
    el("td", {}, el("button", { type: "button", onclick: () => go({ book: book.id }) }, t("filter"))),
  ));
  reader.replaceChildren(
    el("table", {},
      el("thead", {}, el("tr", {}, el("th", { class: "num" }, "#"), el("th", {}, t("book")),
        el("th", { class: "num" }, t("pagesColumn")), el("th", { class: "num" }, t("chunksColumn")), el("th", {}, ""))),
      el("tbody", {}, rows)),
  );
}

async function showReader(state) {
  const reader = document.getElementById("reader");
  const read = state.get("read") || "";
  const [kind, ...args] = read.split(":");
  try {
    if (kind === "page") {
      const [book, page, start] = args.map(Number);
      await showPage(reader, book, page, start || 0);
    } else if (kind === "passage") {
      await showPassage(reader, Number(args[0]));
    } else {
      showBooks(reader);
    }
    reader.scrollTop = 0;
    if (read && matchMedia("(max-width: 860px)").matches) reader.scrollIntoView({ behavior: "smooth" });
  } catch (error) {
    reader.replaceChildren(el("p", { class: "status error" }, error.message));
  }
}

function syncForm(state) {
  document.getElementById("q").value = state.get("q") || "";
  document.getElementById("mode").value = state.get("mode") || "semantic";
  document.getElementById("book").value = state.get("book") || "";
  document.getElementById("limit").value = state.get("limit") || "10";
}

async function route() {
  const state = hashState();
  syncForm(state);
  const search = ["q", "mode", "book", "limit"].map((key) => state.get(key) || "").join("\u0000");
  const read = state.get("read") || "";
  if (search !== shownSearch) {
    shownSearch = search;
    await runSearch(state);
  }
  if (read !== shownReader) {
    shownReader = read;
    await showReader(state);
  }
}

async function start() {
  localize();
  document.getElementById("search").addEventListener("submit", (event) => {
    event.preventDefault();
    const form = new FormData(event.target);
    go({ q: form.get("q").trim(), mode: form.get("mode"), book: form.get("book"), limit: form.get("limit") });
  });
  for (const id of ["mode", "book", "limit"]) {
    document.getElementById(id).addEventListener("change", (event) => {
      if (hashState().get("q")) go({ [id]: event.target.value });
    });
  }
  document.addEventListener("keydown", (event) => {
    const typing = /^(INPUT|SELECT|TEXTAREA)$/.test(document.activeElement?.tagName || "");
    if (event.key === "/" && !typing) {
      event.preventDefault();
      document.getElementById("q").focus();
    }
    const [kind, book, page] = (hashState().get("read") || "").split(":");
    if (!typing && kind === "page" && Number(page) > 0 && (event.key === "ArrowLeft" || event.key === "ArrowRight")) {
      const next = Number(page) + (event.key === "ArrowLeft" ? -1 : 1);
      const count = books.get(Number(book))?.page_count || Infinity;
      if (next >= 1 && next <= count) readPage({ book_id: Number(book), page_number: next });
    }
  });
  window.addEventListener("hashchange", route);
  try {
    const [list, stats] = await Promise.all([api("/api/books"), api("/api/stats")]);
    const select = document.getElementById("book");
    for (const book of list) {
      books.set(book.id, book);
      select.append(el("option", { value: book.id }, `${book.id}. ${book.title}`));
    }
    document.getElementById("stats").textContent = t("stats", stats.counts);
  } catch (error) {
    document.getElementById("stats").textContent = error.message;
  }
  await route();
}

start();
