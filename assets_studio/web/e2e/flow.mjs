import { mkdirSync } from "node:fs";
import { join } from "node:path";
import { URL } from "node:url";
import { chromium } from "playwright-core";

const [base, screens] = process.argv.slice(2);
mkdirSync(screens, { recursive: true });
const browser = await chromium.launch({
  executablePath: process.env.CHROME ?? "google-chrome",
  args: ["--no-sandbox", "--use-gl=swiftshader", "--enable-unsafe-swiftshader"],
});
const page = await browser.newPage({
  viewport: { width: 1440, height: 960 },
  locale: "ru-RU",
});
const errors = [];
page.on("pageerror", (error) => errors.push(error.message));
try {
  await page.goto(base);
  await page
    .getByLabel("Модель и задача")
    .locator('option[value="image|text-to-image"]')
    .waitFor({ state: "attached" });
  await page.getByLabel("Название графа").fill("Проверка сохранённой цепочки");
  await page.getByLabel("Модель и задача").selectOption("image|text-to-image");
  await page
    .getByRole("button", { name: "+ Добавить задачу", exact: true })
    .click();
  await page.getByLabel("Промпт", { exact: true }).fill("A red fox");
  await page.getByLabel("Модель и задача").selectOption("image|image-to-image");
  await page
    .getByRole("button", { name: "+ Добавить задачу", exact: true })
    .click();
  await page
    .getByLabel("Вход image", { exact: true })
    .selectOption({ index: 1 });
  await page.getByRole("button", { name: "Разложить граф" }).click();
  const header = page.locator(".graph-node").nth(1).locator(".node-header");
  const box = await header.boundingBox();
  await page.mouse.move(box.x + 60, box.y + 14);
  await page.mouse.down();
  await page.mouse.move(box.x + 95, box.y + 74);
  await page.mouse.up();
  const moved = await header.boundingBox();
  if (moved.x - box.x < 25 || moved.y - box.y < 50)
    throw new Error("node did not move");
  await page.getByRole("button", { name: "Сохранить", exact: true }).click();
  await page.waitForFunction(() => window.location.search.includes("flow="));
  const savedUrl = page.url();
  await page.reload();
  await page.locator(".graph-node").nth(1).waitFor();
  if ((await page.locator(".graph-links > path").count()) !== 1)
    throw new Error("connection was not saved");
  await page.screenshot({ path: join(screens, "saved-graph.png") });
  await page.getByRole("button", { name: "▶ Запустить", exact: true }).click();
  await page.waitForFunction(
    () => document.querySelectorAll(".graph-node.succeeded").length === 2,
  );
  await page.locator(".graph-node").nth(1).click();
  const resultLink = page.getByRole("link", { name: "Скачать результат 1" });
  await resultLink.waitFor();
  const downloadPromise = page.waitForEvent("download");
  await resultLink.click();
  const download = await downloadPromise;
  if (!download.suggestedFilename().endsWith(".png"))
    throw new Error("output download lost its filename");
  await page.screenshot({ path: join(screens, "completed-run.png") });
  const firstRun = (
    await (
      await page.request.get(
        `${base}/flow-api/flows/${new URL(savedUrl).searchParams.get("flow")}/runs`,
      )
    ).json()
  )[0];
  const firstView = await (
    await page.request.get(`${base}/flow-api/runs/${firstRun.id}`)
  ).json();
  const firstJobs = Object.values(firstView.jobs);
  await page.getByRole("button", { name: "К редактированию" }).click();
  await page.getByRole("button", { name: "▶ Запустить", exact: true }).click();
  await page.waitForFunction(
    () => document.querySelectorAll(".graph-node.succeeded").length === 2,
  );
  const runs = await (
    await page.request.get(
      `${base}/flow-api/flows/${new URL(savedUrl).searchParams.get("flow")}/runs`,
    )
  ).json();
  if (runs.length !== 2 || runs[0].id === runs[1].id)
    throw new Error("repeat did not create a new run");
  const secondView = await (
    await page.request.get(`${base}/flow-api/runs/${runs[0].id}`)
  ).json();
  if (Object.values(secondView.jobs).some((job) => firstJobs.includes(job)))
    throw new Error("repeat reused old jobs");
  if (errors.length) throw new Error(errors.join("\n"));
  console.log(
    "FLOW_UI_OK: edit, connect, drag, save, reload, queue dependencies, download, repeat",
  );
} catch (error) {
  await page.screenshot({ path: join(screens, "failure.png") });
  console.error(error);
  process.exitCode = 1;
} finally {
  await browser.close();
}
