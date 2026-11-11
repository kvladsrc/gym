// Library thumbnails of meshes (ADR-011): pictures drawn once by the browser in
// a worker, kept by the studio and shown again without drawing; no live 3D
// view in the list. tests/test_web_ui.py prepares the library (a model whose
// picture is outdated, one that cannot be drawn) and checks the pictures kept.
import assert from "node:assert/strict";
import { chromium } from "playwright-core";

const [base] = process.argv.slice(2);
const browser = await chromium.launch({
  executablePath: process.env.CHROME ?? "google-chrome",
  args: ["--no-sandbox", "--use-gl=swiftshader", "--enable-unsafe-swiftshader"],
});
const page = await browser.newPage({
  viewport: { width: 1280, height: 800 },
  locale: "ru-RU",
});
const problems = [];
const failures = [];
const kept = [];
page.on("console", (message) => {
  const text = message.text();
  if (text.startsWith("No preview of ")) failures.push(text);
  // Failed requests are reported with their URL by the response listener
  // (the studio has no favicon).
  else if (
    message.type() === "error" &&
    !text.startsWith("Failed to load resource")
  )
    problems.push(`console: ${text}`);
});
page.on("pageerror", (error) => problems.push(`page error: ${error.message}`));
page.on("response", (response) => {
  if (response.status() >= 400)
    problems.push(`HTTP ${response.status()} ${response.url()}`);
});
page.on("request", (request) => {
  if (request.method() === "PUT") kept.push(request.url());
});

async function meshes() {
  const response = await fetch(`${base}/api/assets?kind=mesh`);
  assert.equal(response.status, 200);
  return response.json();
}

/** The pictures the cards show, loaded. */
const shown = () =>
  page.evaluate(() =>
    [...document.querySelectorAll(".card .mesh-thumb img")]
      .filter((img) => img.complete && img.naturalWidth > 0)
      .map((img) => img.getAttribute("src"))
      .sort(),
  );

async function until(check, what, timeout = 60_000) {
  const deadline = Date.now() + timeout;
  while (!(await check())) {
    if (Date.now() > deadline) throw new Error(`timed out waiting: ${what}`);
    await page.waitForTimeout(100);
  }
}

try {
  const listed = await meshes();
  const broken = listed.find((asset) => asset.title === "broken");
  assert.ok(broken, "the library has the broken model");
  const drawable = listed.filter((asset) => asset.id !== broken.id);
  assert.ok(
    drawable.some((asset) => asset.preview_version === 0),
    "one picture is outdated",
  );

  await page.goto(`${base}/#/library`);
  await until(
    async () =>
      (await meshes()).filter((asset) => asset.preview_version === 1).length ===
      drawable.length,
    "every model drawn by this renderer",
  );
  const urls = (await meshes())
    .filter((asset) => asset.id !== broken.id)
    .map((asset) => asset.preview_url)
    .sort();
  await until(
    async () => JSON.stringify(await shown()) === JSON.stringify(urls),
    "the new pictures in the cards",
  );
  await until(() => failures.length === 1, "the broken model given up");
  assert.match(failures[0], new RegExp(broken.id));
  assert.equal(
    await page
      .locator(".card", { hasText: "broken" })
      .locator(".mesh-thumb svg")
      .count(),
    1,
    "a model that cannot be drawn shows a cube",
  );
  assert.equal(kept.length, drawable.length, "one picture kept per model");
  assert.equal(await page.locator("model-viewer").count(), 0);

  // Kept: a reload shows the same pictures without drawing them again.
  kept.length = 0;
  failures.length = 0;
  await page.reload();
  await until(
    async () => JSON.stringify(await shown()) === JSON.stringify(urls),
    "the kept pictures after a reload",
  );
  await until(() => failures.length === 1, "the broken model tried again");
  assert.deepEqual(kept, []);
  assert.deepEqual(problems, []);
  console.log(
    "PASS drawn in a worker, kept, outdated redrawn, failure shown as a cube, no redraw after reload",
  );
} finally {
  await browser.close();
}
