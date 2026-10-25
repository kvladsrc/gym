// Browser walkthrough of the main UI scenarios against a running studio with
// fake model servers (started by tests/test_web_ui.py).
// Usage: node e2e/smoke.mjs <studio url> <screenshot dir>
// CONTROL: URL of the harness that stops and starts the "Основная" image server.
import { mkdirSync } from "node:fs";
import { join } from "node:path";

import { chromium } from "playwright-core";

const [base, screens] = process.argv.slice(2);
if (!base || !screens) {
  console.error("usage: node e2e/smoke.mjs <studio url> <screenshot dir>");
  process.exit(2);
}
mkdirSync(screens, { recursive: true });

const browser = await chromium.launch({
  executablePath: process.env.CHROME ?? "google-chrome",
  args: ["--no-sandbox", "--use-gl=swiftshader", "--enable-unsafe-swiftshader"],
});
// Russian, as the walkthrough's steps read the interface (the English one is
// checked in a step of its own); Russian parameter labels come in x-labels.
const page = await browser.newPage({
  viewport: { width: 1280, height: 800 },
  locale: "ru-RU",
});
const problems = [];
page.on("console", (message) => {
  // Failed requests are reported with their URL by the response listener.
  if (
    message.type() === "error" &&
    !message.text().startsWith("Failed to load resource")
  ) {
    problems.push(`console: ${message.text()}`);
  }
});
page.on("pageerror", (error) => problems.push(`page error: ${error.message}`));
page.on("response", (response) => {
  if (response.status() >= 400)
    problems.push(`HTTP ${response.status()} ${response.url()}`);
});

async function step(name, action) {
  try {
    await action();
    console.log(`PASS ${name}`);
  } catch (error) {
    await page.screenshot({ path: join(screens, `failed-${name}.png`) });
    console.log(`FAIL ${name}: ${error.message}`);
    // The server's view, to tell a stuck queue from a missed UI update.
    const recent = await (await fetch(`${base}/api/jobs?limit=3`)).json();
    console.log(
      `server jobs: ${JSON.stringify(recent.map((job) => [job.task, job.status, job.error_code]))}`,
    );
    await browser.close();
    process.exit(1);
  }
}

async function noHorizontalScroll() {
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - window.innerWidth,
  );
  if (overflow > 0)
    throw new Error(`page scrolls horizontally by ${overflow}px`);
}

const control = process.env.CONTROL;
const rows = page.locator(".jobs .job-row");
const newest = () => rows.first();

/** Submit the form and wait until the new job heads the section's list (and has finished). */
async function generate(status = "succeeded") {
  const before = (await rows.count())
    ? await newest().getAttribute("data-job")
    : null;
  await page.getByRole("button", { name: "Сгенерировать" }).click();
  await page.waitForFunction((previous) => {
    const row = document.querySelector(".jobs .job-row");
    return row !== null && row.getAttribute("data-job") !== previous;
  }, before);
  if (status) await newest().locator(`.badge.${status}`).waitFor();
}

/** The newest job's result, opened in the library. */
async function openResult() {
  await newest()
    .getByRole("link", { name: /Открыть в библиотеке/ })
    .click();
  await page.locator(".details .preview").waitFor();
}

/** The model tree's button for a task of a section: its first model, or the named one. */
function treeModel(section, task, model) {
  const folder = page
    .locator(".tree > ul > li")
    .filter({ has: page.locator(".tree-folder", { hasText: section }) });
  const item = folder.locator("li").filter({
    has: page.locator(".tree-task", { hasText: new RegExp(`^${task}$`) }),
  });
  const models = item.locator(".tree-model");
  return model ? models.filter({ hasText: model }) : models.first();
}

/** Wait until the form of this task is on screen. */
async function onTask(task) {
  await page
    .locator(".launch-head h1", { hasText: new RegExp(`^${task}$`) })
    .waitFor();
}

/** Open a task's form from the tree (from any page: Generate shows the tree). */
async function pick(section, task, model) {
  if (!(await page.locator(".tree").count()))
    await page.getByRole("link", { name: "Генерация", exact: true }).click();
  await treeModel(section, task, model).click();
  await onTask(task);
}

async function openLibrary() {
  await page.getByRole("link", { name: "Библиотека", exact: true }).click();
  await page.locator(".grid .card").first().waitFor();
}

async function advanced() {
  const details = page.locator("details");
  // The attribute is present (possibly empty) when open.
  if ((await details.getAttribute("open")) === null) {
    await details.locator("summary").click();
  }
}

await step("home lists models never started; sections by result", async () => {
  await page.goto(base);
  await page.getByRole("heading", { name: "Ещё не запускались" }).waitFor();
  await page.locator(".unseen", { hasText: "Офлайн" }).waitFor();
  await page.getByRole("link", { name: "Генерация", exact: true }).click();
  await page.locator(".tree .tree-folder").first().waitFor();
  const titles = await page.locator(".tree .tree-folder").count();
  if (titles !== 5) throw new Error(`expected 5 sections, got ${titles}`);
  await page.locator(".tree-model .dot.ready").first().waitFor();
  // Two models can make images: both under the task in the tree.
  const models = await treeModel("Картинки", "Текст → картинка")
    .locator("xpath=../..")
    .locator(".tree-model")
    .count();
  if (models !== 2) throw new Error(`expected 2 models, got ${models}`);
});

await step("generate three image variants", async () => {
  await pick("Картинки", "Текст → картинка", "Основная");
  await page.locator(".model-panel", { hasText: "Fake model" }).waitFor(); // what the model is
  await page.locator("textarea").fill("мох на камне");
  await page.getByRole("button", { name: "3", exact: true }).click();
  await generate();
  // Three results: one job line, the results in the library.
  await newest()
    .getByRole("link", { name: "Открыть в библиотеке (3)" })
    .waitFor();
  await noHorizontalScroll();
  await page.screenshot({ path: join(screens, "1-image.png") });
  await openResult();
  await page.locator(".details .preview img").waitFor();
  await page.locator(".details", { hasText: "Fake model (fake)" }).waitFor();
  await page.getByRole("link", { name: "Генерация", exact: true }).click();
  await onTask("Текст → картинка");
});

await step("the same task with another model", async () => {
  await pick("Картинки", "Текст → картинка", "Вторая");
  const kept = await page.locator("textarea").inputValue();
  if (kept !== "мох на камне") throw new Error(`the text was lost: "${kept}"`);
  await page.getByRole("button", { name: "1", exact: true }).click();
  await generate();
  await page.locator(".model-panel h2", { hasText: "Вторая" }).waitFor();
  await pick("Картинки", "Текст → картинка", "Основная");
});

await step("send a result to another task of the same section", async () => {
  await openResult();
  await page.getByRole("button", { name: "→ Картинки", exact: true }).click();
  await onTask("Картинка → картинка");
  await page.locator(".dropzone .thumb img").waitFor(); // the input was kept
});

await step("decimal parameters can be typed", async () => {
  await advanced();
  const strength = page.getByLabel("Сила изменения");
  await strength.fill("");
  await strength.pressSequentially("5");
  await page.getByText("Нужно число от 0 до 1").waitFor();
  if (await page.getByRole("button", { name: "Сгенерировать" }).isEnabled()) {
    throw new Error("an out-of-range value must block sending");
  }
  await strength.fill("");
  await strength.pressSequentially("0.25");
  const typed = await strength.inputValue();
  if (typed !== "0.25") throw new Error(`typed 0.25, field shows ${typed}`);
  await generate();
  await noHorizontalScroll();
  await page.screenshot({ path: join(screens, "2-image-to-image.png") });
  // Iterating: sending the result back keeps the task and the tuned strength.
  await openResult();
  await page.getByRole("button", { name: "→ Картинки", exact: true }).click();
  await onTask("Картинка → картинка");
  const kept = await page.getByLabel("Сила изменения").inputValue();
  if (kept !== "0.25") throw new Error(`strength reset to ${kept}`);
});

await step("send the image to the 3D section", async () => {
  await openResult();
  await page.getByRole("button", { name: "→ 3D", exact: true }).click();
  await page.waitForURL(/#\/mesh$/);
  await onTask("Картинка → 3D");
  await page.locator(".dropzone .thumb img").waitFor();
  await generate();
  await openResult();
  await page.locator(".details .preview model-viewer").waitFor();
  await page.waitForTimeout(1500); // let the model render for the screenshot
  await noHorizontalScroll();
  await page.screenshot({ path: join(screens, "3-mesh.png") });
});

await step("speech, sound and sound-to-sound", async () => {
  await pick("Звук", "Текст → речь");
  await page.locator("textarea").fill("Привет, путник!");
  await generate();
  await openResult();
  await page.getByRole("button", { name: "Слушать" }).waitFor();
  await pick("Звук", "Текст → звук");
  await page.locator("textarea").fill("шаги по гравию");
  await generate();
  await openResult();
  await page.getByRole("button", { name: "→ Звук", exact: true }).click();
  await onTask("Звук → звук");
  await page.locator(".dropzone .thumb svg").waitFor();
  await generate();
  await openResult();
  await page.getByRole("button", { name: "Слушать" }).waitFor();
  await page.waitForTimeout(500);
  await noHorizontalScroll();
  await page.screenshot({ path: join(screens, "4-audio.png") });
});

await step(
  "text, video and text-to-3D; primary parameters up front",
  async () => {
    await pick("Текст", "Текст → текст");
    // "Слов" is marked primary: shown without opening "Дополнительно".
    if ((await page.locator("details").getAttribute("open")) !== null)
      await page.locator("details summary").click();
    await page.getByLabel("Слов").fill("5");
    await page.locator("textarea").fill("описание таверны");
    await generate();
    await openResult();
    const text = await page.locator(".details .preview .text pre").innerText();
    if (text.trim().split(/\s+/).length !== 5)
      throw new Error(`expected 5 words, got ${JSON.stringify(text)}`);
    await page
      .getByRole("button", { name: "Копировать", exact: true })
      .waitFor();
    // Text-to-text is X-to-X: the result goes back in as the text to edit.
    await page.getByRole("button", { name: "→ Текст", exact: true }).click();
    await page.locator(".dropzone .thumb svg").waitFor();
    await page.locator("textarea").fill("продолжи");
    await generate();
    await openResult();
    const edited = await page
      .locator(".details .preview .text pre")
      .innerText();
    if (!edited.startsWith(text.trim()))
      throw new Error("the source text is not part of the result");
    await page.screenshot({ path: join(screens, "4a-text.png") });

    await pick("Видео", "Картинка → видео");
    await page.getByRole("button", { name: "Из библиотеки" }).click();
    await page.getByRole("dialog").locator(".card").first().click();
    await page.locator(".dropzone .thumb img").waitFor();
    await generate();
    await openResult();
    await page.locator(".details .preview video").waitFor();
    await page.waitForFunction(
      () => document.querySelector(".details .preview video")?.readyState >= 1,
    ); // the browser can decode the clip
    // Any frame can be kept as an image made from the video.
    await page
      .getByRole("button", { name: "Сохранить кадр картинкой" })
      .click();
    await page.getByText(/Кадр на .* сохранён картинкой/).waitFor();
    await noHorizontalScroll();
    await page.screenshot({ path: join(screens, "4b-video.png") });

    await pick("3D", "Текст → 3D");
    await page.locator("textarea").fill("бочка");
    await generate();
    await openResult();
    await page.locator(".details .preview model-viewer").waitFor();
  },
);

await step("drafts survive switching sections", async () => {
  await pick("Картинки", "Текст → картинка");
  await page.locator("textarea").fill("черновик");
  await pick("3D", "Текст → 3D");
  await pick("Картинки", "Текст → картинка");
  const kept = await page.locator("textarea").inputValue();
  if (kept !== "черновик") throw new Error(`draft lost: "${kept}"`);
  // An invalid number survives a tab switch, blocks sending and says why.
  await advanced();
  await page.getByLabel("Width").fill("abc");
  await page.getByText("Исправьте значения в «Дополнительно»").waitFor();
  await pick("3D", "Текст → 3D");
  await pick("Картинки", "Текст → картинка");
  const width = await page.getByLabel("Width").inputValue();
  if (width !== "abc") throw new Error(`invalid text lost: "${width}"`);
  if (await page.getByRole("button", { name: "Сгенерировать" }).isEnabled()) {
    throw new Error("an invalid value must block sending");
  }
  // Another task starts from its own defaults: the invalid text is gone.
  await pick("Картинки", "Картинка → картинка");
  const reset = await page.getByLabel("Width").inputValue();
  if (reset !== "256")
    throw new Error(`expected the default 256, got "${reset}"`);
  await page
    .getByText("Исправьте значения в «Дополнительно»")
    .waitFor({ state: "detached" });
});

await step("pick an input from the library dialog", async () => {
  const remove = page.getByRole("button", { name: "Убрать" });
  if (await remove.count()) await remove.click();
  await page.getByRole("button", { name: "Из библиотеки" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.waitFor();
  await page.keyboard.press("Escape");
  await dialog.waitFor({ state: "detached" });
  await page.getByRole("button", { name: "Из библиотеки" }).click();
  await dialog.locator(".card").first().click();
  await page.locator(".dropzone .thumb img").waitFor();
});

await step(
  "a stopped server: indicator, waiting and cancelled jobs",
  async () => {
    // A model chosen by hand sticks: its jobs wait for it rather than go to
    // the other one, which is still ready.
    await pick("Картинки", "Картинка → картинка", "Основная");
    const main = treeModel("Картинки", "Картинка → картинка", "Основная");
    await fetch(`${control}/stop`, { method: "POST" });
    await main.locator(".dot.unavailable").waitFor();
    // The other image model still runs: the section stays usable.
    await treeModel("Картинки", "Текст → картинка", "Вторая")
      .locator(".dot.ready")
      .waitFor();
    await page
      .locator(".panel .hint", { hasText: "Основная: сервер не запущен" })
      .waitFor();
    await pick("Картинки", "Текст → картинка", "Основная"); // tasks are remembered
    await page.locator("textarea").fill("ждёт сервер");
    await generate(null);
    await page.getByText("Ждёт запуска модели").waitFor();
    await newest().getByRole("button", { name: "Отменить" }).click();
    await newest().locator(".badge.cancelled").waitFor();
    await noHorizontalScroll();
    await page.screenshot({ path: join(screens, "5-stopped.png") });
    await generate(null);
    await page.getByText("Ждёт запуска модели").waitFor();
    await fetch(`${control}/start`, { method: "POST" });
    await treeModel("Картинки", "Текст → картинка", "Основная")
      .locator(".dot.ready")
      .waitFor();
    await newest().locator(".badge.succeeded").waitFor();
  },
);

await step("a failed job and its retry", async () => {
  await advanced();
  await page.getByLabel("Имитация ошибки").selectOption("generation");
  await generate("failed");
  await newest().locator(".error-box").getByText("generation_failed").waitFor();
  const failed = await newest().getAttribute("data-job");
  const before = await rows.count();
  await newest().getByRole("button", { name: "Повторить" }).click();
  // The retry replaces the failed job in the list (ADR-004).
  await page.waitForFunction(
    ([id, count]) =>
      document.querySelectorAll(".jobs .job-row").length === count &&
      !document.querySelector(`.jobs .job-row[data-job="${id}"]`),
    [failed, before],
  );
  await page.screenshot({ path: join(screens, "6-failed.png") });
  await page.getByLabel("Имитация ошибки").selectOption("none");
});

await step("the jobs page: every model's queue and history", async () => {
  await page.getByRole("link", { name: "Задания", exact: true }).click();
  const history = page.locator(".jobs-page .jobs").nth(1);
  await history.locator(".job-row").first().waitFor();
  // Jobs of several models and tasks, each line naming its task and model.
  await history
    .locator(".job-row .job-task", { hasText: "Текст → картинка" })
    .first()
    .waitFor();
  await history
    .locator(".job-row .job-task", { hasText: "Текст → речь" })
    .first()
    .waitFor();
  await history.getByRole("button", { name: "Ошибка" }).click();
  await page.waitForFunction(
    () =>
      [
        ...document.querySelectorAll(
          ".jobs-page .jobs:nth-of-type(2) .job-row",
        ),
      ].every((row) => row.classList.contains("failed")) &&
      document.querySelector(".jobs-page .jobs:nth-of-type(2) .job-row") !==
        null,
  );
  await noHorizontalScroll();
  await page.screenshot({ path: join(screens, "6a-jobs.png") });
});

await step("a server that never started", async () => {
  await page.getByRole("link", { name: "Не запускались: 1" }).click();
  await page.locator(".unseen", { hasText: "Офлайн" }).waitFor();
  await noHorizontalScroll();
  await page.screenshot({ path: join(screens, "7-offline.png") });
});

await step("library lists results and shows details", async () => {
  await openLibrary();
  const cards = await page.locator(".grid .card").count();
  if (cards < 8) throw new Error(`expected at least 8 assets, got ${cards}`);
  await page.getByRole("button", { name: "Картинки" }).click();
  await page.locator(".grid .card").first().click();
  await page.locator(".details .preview img").waitFor();
  await page
    .locator(".details")
    .getByRole("button", { name: "В избранное" })
    .click();
  await page
    .locator(".details")
    .getByRole("button", { name: "Убрать из избранного" })
    .waitFor();
  // Tags are chips: add one, then filter the library by clicking it.
  const details = page.locator(".details");
  await details.getByRole("button", { name: "+ Тег" }).click();
  await details.getByRole("textbox", { name: "+ Тег" }).fill("Style:Cartoon");
  await details.getByRole("textbox", { name: "+ Тег" }).press("Enter");
  await details
    .getByRole("button", { name: "style:cartoon", exact: true })
    .click();
  await page
    .locator(".filters")
    .getByRole("button", { name: "Убрать тег style:cartoon" })
    .click();
  await page.getByRole("button", { name: "★ Избранное" }).click();
  await page.waitForFunction(
    () => document.querySelectorAll(".grid .card").length === 1,
  );
  await noHorizontalScroll();
  await page.screenshot({ path: join(screens, "8-library.png") });
});

await step("delete an asset from the library", async () => {
  await page.getByRole("button", { name: "Все" }).click();
  // The favourites grid (one card) is replaced by the full list.
  await page.waitForFunction(
    () => document.querySelectorAll(".grid .card").length > 1,
  );
  const before = await page.locator(".grid .card").count();
  await page.locator(".grid .card").first().click();
  await page
    .locator(".details")
    .getByRole("button", { name: "Удалить" })
    .click();
  await page
    .locator(".details")
    .getByRole("button", { name: "Точно удалить?" })
    .click();
  await page.getByText("Удалено из библиотеки").waitFor();
  await page.waitForFunction(
    (count) => document.querySelectorAll(".grid .card").length === count,
    before - 1,
  );
  await page.locator(".details").waitFor({ state: "detached" });
});

await step(
  "a job whose results are all deleted leaves the history",
  async () => {
    await pick("Звук", "Звук → звук");
    await newest().waitFor();
    const before = await rows.count();
    await openResult();
    const details = page.locator(".details");
    await details.getByRole("button", { name: "Удалить" }).click();
    await details.getByRole("button", { name: "Точно удалить?" }).click();
    await page.getByText("Удалено из библиотеки").waitFor();
    await pick("Звук", "Звук → звук");
    await page.waitForFunction(
      (count) => document.querySelectorAll(".jobs .job-row").length === count,
      before - 1,
    );
  },
);

await step("dark theme: chosen, applied and remembered", async () => {
  const background = () =>
    page.evaluate(
      () => window.getComputedStyle(document.documentElement).backgroundColor,
    );
  const theme = page.locator("button.theme");
  while ((await theme.textContent()) !== "Тема: тёмная") await theme.click();
  if ((await background()) !== "rgb(21, 24, 29)")
    throw new Error(`dark background expected, got ${await background()}`);
  await pick("Картинки", "Текст → картинка");
  await page.screenshot({ path: join(screens, "10-dark-section.png") });
  await page.getByRole("link", { name: "Библиотека", exact: true }).click();
  await page.locator(".grid .card").first().waitFor();
  await page.screenshot({ path: join(screens, "11-dark-library.png") });
  await page.reload();
  await page.locator("button.theme", { hasText: "Тема: тёмная" }).waitFor();
  if ((await background()) !== "rgb(21, 24, 29)")
    throw new Error("the dark theme was not remembered");
  while ((await theme.textContent()) !== "Тема: как в системе")
    await theme.click();
});

await step("an image at its real size", async () => {
  await openLibrary();
  await page.getByRole("button", { name: "Картинки" }).click();
  await page.locator(".grid .card").first().click();
  await page.locator(".details .preview img.zoomable").click();
  const viewer = page.getByRole("dialog");
  await viewer.getByRole("button", { name: "100 %" }).click();
  const [shown, natural] = await viewer
    .locator("img")
    .evaluate((image) => [image.clientWidth, image.naturalWidth]);
  if (shown !== natural)
    throw new Error(`100 % should be ${natural} px wide, got ${shown}`);
  await page.screenshot({ path: join(screens, "12-real-size.png") });
  await page.keyboard.press("Escape");
  await viewer.waitFor({ state: "detached" });
  // Focus is back on the image that opened the viewer.
  if (
    !(await page.evaluate(() =>
      document.activeElement?.classList.contains("zoomable"),
    ))
  )
    throw new Error("focus did not return to the image");
});

await step("English interface, remembered", async () => {
  await pick("Картинки", "Текст → картинка");
  await page.getByRole("button", { name: "English" }).click();
  await page.getByRole("link", { name: "Library", exact: true }).waitFor();
  await page.getByRole("button", { name: "Generate" }).waitFor();
  // The servers' own (English) labels instead of their Russian x-labels.
  await page.getByText("Advanced").click();
  await page.getByText("Artificial delay, s").waitFor();
  if ((await page.evaluate(() => document.documentElement.lang)) !== "en")
    throw new Error("<html lang> did not follow the language");
  await page.screenshot({ path: join(screens, "13-english.png") });
  await page.reload();
  await page.getByRole("link", { name: "Library", exact: true }).waitFor();
  await page.getByRole("button", { name: "Русский" }).click();
  await page.getByRole("link", { name: "Библиотека", exact: true }).waitFor();
});

await browser.close();
const unexpected = problems.filter((problem) => !problem.includes("favicon"));
if (unexpected.length) {
  console.log(`FAIL browser errors:\n${unexpected.join("\n")}`);
  process.exit(1);
}
console.log("PASS no browser errors");
