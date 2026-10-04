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
const history = page.locator(".history .thumb");

/** Submit the form and wait until the new job is the one shown (and finished). */
async function generate(status = "succeeded") {
  const before = await history.count();
  await page.getByRole("button", { name: "Сгенерировать" }).click();
  await page.waitForFunction(
    (count) =>
      document.querySelectorAll(".history .thumb").length === count &&
      document.querySelector(".history .thumb")?.classList.contains("selected"),
    before + 1,
  );
  if (status) await page.locator(`.job-head .badge.${status}`).waitFor();
}

/** Wait until the section is really on screen: the previous page may still be rendered. */
async function onSection(name) {
  await page.locator(".nav a.active", { hasText: name }).waitFor();
}

async function openSection(name) {
  await page.locator(".nav a", { hasText: name }).click();
  await onSection(name);
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
  const titles = await page.locator(".nav a .dot").count();
  if (titles !== 5) throw new Error(`expected 5 sections, got ${titles}`);
  await openSection("Картинки");
  await page.locator(".nav a.active .dot.ready").waitFor();
  // Two models can make images: the choice is inside the section.
  const models = await page.locator(".models button").count();
  if (models !== 2) throw new Error(`expected 2 models, got ${models}`);
});

await step("generate three image variants", async () => {
  await page.getByRole("button", { name: "Текст → картинка" }).click();
  await page.locator("textarea").fill("мох на камне");
  await page.getByRole("button", { name: "3", exact: true }).click();
  await generate();
  const candidates = await page.locator(".candidates .thumb").count();
  if (candidates !== 3)
    throw new Error(`expected 3 candidates, got ${candidates}`);
  await page.getByRole("button", { name: "Вариант 2" }).click();
  await page.locator(".preview img").waitFor();
  await page
    .locator(".actions .hint", { hasText: "Fake model (fake)" })
    .waitFor();
  await noHorizontalScroll();
  await page.screenshot({ path: join(screens, "1-image.png") });
});

await step("the same task with another model", async () => {
  await page.getByRole("button", { name: "Вторая" }).click();
  await page.getByRole("button", { name: "1", exact: true }).click();
  await generate();
  await page
    .locator(".actions .hint", { hasText: "Fake model (second)" })
    .waitFor();
  const kept = await page.locator("textarea").inputValue();
  if (kept !== "мох на камне") throw new Error(`the text was lost: "${kept}"`);
  await page.getByRole("button", { name: "Основная" }).click();
  await page
    .getByRole("button", { name: "Вариант 2" })
    .waitFor({ state: "detached" });
});

await step("send a result to another task of the same section", async () => {
  await page.getByRole("button", { name: "→ Картинки", exact: true }).click();
  await page
    .locator('.tasks button[aria-pressed="true"]', {
      hasText: "Картинка → картинка",
    })
    .waitFor();
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
  await page.getByRole("button", { name: "→ Картинки", exact: true }).click();
  await page
    .locator('.tasks button[aria-pressed="true"]', {
      hasText: "Картинка → картинка",
    })
    .waitFor();
  const kept = await page.getByLabel("Сила изменения").inputValue();
  if (kept !== "0.25") throw new Error(`strength reset to ${kept}`);
});

await step("send the image to the 3D section", async () => {
  await page.getByRole("button", { name: "→ 3D", exact: true }).click();
  await page.waitForURL(/#\/mesh$/);
  await onSection("3D");
  await page.locator(".dropzone .thumb img").waitFor();
  await generate();
  await page.locator(".preview model-viewer").waitFor();
  await page.waitForTimeout(1500); // let the model render for the screenshot
  await noHorizontalScroll();
  await page.screenshot({ path: join(screens, "3-mesh.png") });
});

await step("speech, sound and sound-to-sound", async () => {
  await openSection("Звук");
  await page.getByRole("button", { name: "Текст → речь" }).click();
  await page.locator("textarea").fill("Привет, путник!");
  await generate();
  await page.getByRole("button", { name: "Слушать" }).waitFor();
  await page.getByRole("button", { name: "Текст → звук" }).click();
  await page.locator("textarea").fill("шаги по гравию");
  await generate();
  await page.getByRole("button", { name: "→ Звук", exact: true }).click();
  await page
    .locator('.tasks button[aria-pressed="true"]', { hasText: "Звук → звук" })
    .waitFor();
  await page.locator(".dropzone .thumb svg").waitFor();
  await generate();
  await page.getByRole("button", { name: "Слушать" }).waitFor();
  await page.waitForTimeout(500);
  await noHorizontalScroll();
  await page.screenshot({ path: join(screens, "4-audio.png") });
});

await step(
  "text, video and text-to-3D; primary parameters up front",
  async () => {
    await openSection("Текст");
    // "Слов" is marked primary: shown without opening "Дополнительно".
    if ((await page.locator("details").getAttribute("open")) !== null)
      await page.locator("details summary").click();
    await page.getByLabel("Слов").fill("5");
    await page.locator("textarea").fill("описание таверны");
    await generate();
    const text = await page.locator(".preview .text pre").innerText();
    if (text.trim().split(/\s+/).length !== 5)
      throw new Error(`expected 5 words, got ${JSON.stringify(text)}`);
    await page.getByRole("button", { name: "Копировать" }).waitFor();
    // Text-to-text is X-to-X: the result goes back in as the text to edit.
    await page.getByRole("button", { name: "→ Текст", exact: true }).click();
    await page.locator(".dropzone .thumb svg").waitFor();
    await page.locator("textarea").fill("продолжи");
    await generate();
    const edited = await page.locator(".preview .text pre").innerText();
    if (!edited.startsWith(text.trim()))
      throw new Error("the source text is not part of the result");
    await page.screenshot({ path: join(screens, "4a-text.png") });

    await openSection("Видео");
    await page.getByRole("button", { name: "Из библиотеки" }).click();
    await page.getByRole("dialog").locator(".card").first().click();
    await page.locator(".dropzone .thumb img").waitFor();
    await generate();
    await page.locator(".preview video").waitFor();
    await page.waitForFunction(
      () => document.querySelector(".preview video")?.readyState >= 1,
    ); // the browser can decode the clip
    await page.locator(".history .thumb video").first().waitFor();
    await noHorizontalScroll();
    await page.screenshot({ path: join(screens, "4b-video.png") });

    await openSection("3D");
    await page.getByRole("button", { name: "Текст → 3D" }).click();
    await page.locator("textarea").fill("бочка");
    await generate();
    await page.locator(".preview model-viewer").waitFor();
  },
);

await step("drafts survive switching sections", async () => {
  await openSection("Картинки");
  await page.locator("textarea").fill("черновик");
  await openSection("3D");
  await openSection("Картинки");
  const kept = await page.locator("textarea").inputValue();
  if (kept !== "черновик") throw new Error(`draft lost: "${kept}"`);
  // An invalid number survives a tab switch, blocks sending and says why.
  await advanced();
  await page.getByLabel("Width").fill("abc");
  await page.getByText("Исправьте значения в «Дополнительно»").waitFor();
  await openSection("3D");
  await openSection("Картинки");
  const width = await page.getByLabel("Width").inputValue();
  if (width !== "abc") throw new Error(`invalid text lost: "${width}"`);
  if (await page.getByRole("button", { name: "Сгенерировать" }).isEnabled()) {
    throw new Error("an invalid value must block sending");
  }
  // Another task starts from its own defaults: the invalid text is gone.
  await page.getByRole("button", { name: "Текст → картинка" }).click();
  await page.getByRole("button", { name: "Картинка → картинка" }).click();
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
    const main = page.locator(".models button", { hasText: "Основная" });
    await main.click();
    await fetch(`${control}/stop`, { method: "POST" });
    await main.locator(".dot.unavailable").waitFor();
    // The other image model still runs: the section stays usable.
    await page
      .locator(".nav a", { hasText: "Картинки" })
      .locator(".dot.ready")
      .waitFor();
    await page
      .locator(".panel .hint", { hasText: "Основная: сервер не запущен" })
      .waitFor();
    await page.getByRole("button", { name: "Текст → картинка" }).click(); // tasks are remembered
    await page.locator("textarea").fill("ждёт сервер");
    await generate(null);
    await page.getByText("Ждёт запуска модели").waitFor();
    await page.getByRole("button", { name: "Отменить" }).click();
    await page.locator(".job-head .badge.cancelled").waitFor();
    await noHorizontalScroll();
    await page.screenshot({ path: join(screens, "5-stopped.png") });
    await generate(null);
    await page.getByText("Ждёт запуска модели").waitFor();
    await fetch(`${control}/start`, { method: "POST" });
    await main.locator(".dot.ready").waitFor();
    await page.locator(".job-head .badge.succeeded").waitFor();
  },
);

await step("a failed job and its retry", async () => {
  await advanced();
  await page.getByLabel("Имитация ошибки").selectOption("generation");
  await generate("failed");
  await page.locator(".error-box").getByText("generation_failed").waitFor();
  const before = await history.count();
  await page.getByRole("button", { name: "Повторить" }).click();
  // The retry replaces the failed job in the history (ADR-004).
  await page.waitForFunction(
    (count) =>
      document.querySelectorAll(".history .thumb").length === count &&
      document
        .querySelector(".history .thumb")
        ?.classList.contains("selected") &&
      !document.querySelector(".error-box"),
    before,
  );
  await page.screenshot({ path: join(screens, "6-failed.png") });
  await page.getByLabel("Имитация ошибки").selectOption("none");
});

await step("a server that never started", async () => {
  await page.getByRole("link", { name: "Не запускались: 1" }).click();
  await page.locator(".unseen", { hasText: "Офлайн" }).waitFor();
  await noHorizontalScroll();
  await page.screenshot({ path: join(screens, "7-offline.png") });
});

await step("library lists results and shows details", async () => {
  await openSection("Библиотека");
  await page.locator(".grid .card").first().waitFor();
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
    await openSection("Звук");
    await history.first().waitFor();
    const before = await history.count();
    await history.first().click();
    const stage = page.locator(".stage");
    await stage.getByRole("button", { name: "Удалить" }).click();
    await stage.getByRole("button", { name: "Точно удалить?" }).click();
    await page.waitForFunction(
      (count) => document.querySelectorAll(".history .thumb").length === count,
      before - 1,
    );
    // The stage moved on to a job that still has its result.
    await stage.getByRole("button", { name: "Слушать" }).waitFor();
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
  await openSection("Картинки");
  await page.screenshot({ path: join(screens, "10-dark-section.png") });
  await page.getByRole("link", { name: "Библиотека" }).click();
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
  await openSection("Картинки");
  // A job with a picture (the newest ones here are failures and retries).
  await page.locator(".history .thumb:has(img)").first().click();
  await page.locator(".preview img.zoomable").click();
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
  await openSection("Картинки");
  await page.getByRole("button", { name: "English" }).click();
  await page.getByRole("link", { name: "Library" }).waitFor();
  await page.getByRole("button", { name: "Generate" }).waitFor();
  // The servers' own (English) labels instead of their Russian x-labels.
  await page.getByText("Advanced").click();
  await page.getByText("Artificial delay, s").waitFor();
  if ((await page.evaluate(() => document.documentElement.lang)) !== "en")
    throw new Error("<html lang> did not follow the language");
  await page.screenshot({ path: join(screens, "13-english.png") });
  await page.reload();
  await page.getByRole("link", { name: "Library" }).waitFor();
  await page.getByRole("button", { name: "Русский" }).click();
  await page.getByRole("link", { name: "Библиотека" }).waitFor();
});

await browser.close();
const unexpected = problems.filter((problem) => !problem.includes("favicon"));
if (unexpected.length) {
  console.log(`FAIL browser errors:\n${unexpected.join("\n")}`);
  process.exit(1);
}
console.log("PASS no browser errors");
