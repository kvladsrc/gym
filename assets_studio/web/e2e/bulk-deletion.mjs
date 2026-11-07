// Bulk deletion against the isolated test library, including a real AssetInUse conflict.
import assert from "node:assert/strict";
import { URL } from "node:url";
import { chromium } from "playwright-core";

const [base] = process.argv.slice(2);
assert.equal(new URL(base).hostname, "127.0.0.1");
assert.notEqual(new URL(base).port, "9000");
const browser = await chromium.launch({
  executablePath: process.env.CHROME ?? "google-chrome",
  args: ["--no-sandbox", "--use-gl=swiftshader", "--enable-unsafe-swiftshader"],
});
const page = await browser.newPage({
  viewport: { width: 1280, height: 800 },
  locale: "en-US",
});
page.setDefaultTimeout(10000);
const errors = [];
page.on("pageerror", (error) => errors.push(error.message));
async function api(path, method = "GET", body) {
  const response = await fetch(`${base}/api/${path}`, {
    method,
    headers: { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  assert.ok(response.ok, `${method} ${path}: ${await response.clone().text()}`);
  return response.json();
}
const button = (name) => page.getByRole("button", { name, exact: true });
const cards = page.locator(".grid .card");
const checkboxes = page.locator(".asset-select");
async function selected(n) {
  await page.waitForFunction(
    (count) =>
      document.querySelectorAll(".asset-select:checked").length === count,
    n,
  );
  assert.match(
    await page.locator(".selection-actions").innerText(),
    new RegExp(`Selected: ${n}\\b`),
  );
}
let job;
try {
  const original = await api("assets?project=default");
  const protectedAsset = original.at(-1);
  await api(`assets/${original[0].id}`, "PATCH", { favorite: true });
  await api(`assets/${original[1].id}`, "PATCH", { favorite: true });
  await api("projects", "POST", { title: "Other" });
  await api(`assets/${original[0].id}`, "PATCH", { project: "other" });
  await page.goto(`${base}/#/library`);
  await page.waitForFunction(
    () => document.querySelectorAll(".grid .card").length === 6,
  );

  // Plain clicks still preview; selection checkboxes are independent controls.
  await cards.first().click();
  await page.locator(".details").waitFor();
  await selected(0);
  const previewURL = page.url();
  await checkboxes.first().click();
  await selected(1);
  assert.equal(page.url(), previewURL);
  await cards.nth(2).click({ modifiers: ["Shift"] });
  await selected(3);
  await cards.nth(1).click({ modifiers: ["Control"] });
  await selected(2);
  await cards.nth(1).click({ modifiers: ["Meta"] });
  await selected(3);
  await page.keyboard.press("Escape");
  await selected(0);
  await cards.nth(3).click({ modifiers: ["Control"] });
  await cards.nth(1).click({ modifiers: ["Shift"] });
  await selected(3);
  await button("Clear").click();
  await selected(0);

  // Filters and project changes clear selection; select-all uses only the shown list.
  await button("Select all shown").click();
  await selected(6);
  await button("★ Favourites").click();
  await page.waitForFunction(
    () => document.querySelectorAll(".grid .card").length === 1,
  );
  await selected(0);
  await button("Select all shown").click();
  await selected(1);
  await button("All").click();
  await page.waitForFunction(
    () => document.querySelectorAll(".grid .card").length === 6,
  );
  await selected(0);
  await button("Select all shown").click();
  await page
    .getByRole("combobox", { name: "Project", exact: true })
    .first()
    .selectOption("other");
  await page.waitForFunction(
    () => document.querySelectorAll(".grid .card").length === 1,
  );
  await selected(0);
  await button("Select all shown").click();
  await selected(1);
  await page
    .getByRole("combobox", { name: "Project", exact: true })
    .first()
    .selectOption("default");
  await page.waitForFunction(
    () => document.querySelectorAll(".grid .card").length === 6,
  );
  await selected(0);

  // A changed selection invalidates confirmation, even if the count is the same.
  await checkboxes.first().click();
  await button("Delete selected (1)").click();
  await button("Confirm deletion (1)?").waitFor();
  await cards.nth(1).click({ modifiers: ["Control"] });
  await cards.first().click({ modifiers: ["Control"] });
  await button("Delete selected (1)").waitFor();
  await button("Select all shown").click();
  await button("Delete selected (6)").click();
  await button("Confirm deletion (6)?").blur();
  await button("Delete selected (6)").waitFor();
  assert.equal((await api("assets?project=default")).length, 6);

  // The fake model holds one input until cancelled: the deletion must return 409.
  await page.waitForFunction(async () => {
    const servers = await (await fetch("/api/servers")).json();
    return servers.some(
      (server) => server.id === "image" && server.tasks.length,
    );
  });
  job = await api("jobs", "POST", {
    server: "image",
    task: "image-to-image",
    inputs: { image: protectedAsset.id },
    params: { delay_s: 60 },
  });

  // Hold the first four requests to prove bounded concurrency and visible progress.
  let active = 0;
  let maximum = 0;
  let release;
  const gate = new Promise((resolve) => {
    release = resolve;
  });
  await page.route("**/api/assets/*", async (route) => {
    if (route.request().method() !== "DELETE") return route.continue();
    active++;
    maximum = Math.max(maximum, active);
    await gate;
    const response = await route.fetch();
    await route.fulfill({ response });
    active--;
  });
  await button("Delete selected (6)").click();
  await button("Confirm deletion (6)?").click();
  await button("Deleting 0/6…").waitFor();
  assert.equal(await button("Deleting 0/6…").isDisabled(), true);
  assert.equal(await button("Clear").isDisabled(), true);
  assert.equal(await checkboxes.first().isDisabled(), true);
  await page.waitForTimeout(150);
  assert.equal(active, 4);
  release();

  await page
    .locator(".notice.error", { hasText: "Deleted 5, failed 1" })
    .waitFor();
  assert.match(await page.locator(".notice").innerText(), new RegExp(job.id));
  await selected(1);
  assert.equal(await cards.count(), 1);
  assert.equal(await button("Delete selected (1)").isEnabled(), true);
  assert.equal(maximum, 4);
  const remaining = await api("assets?project=default");
  assert.deepEqual(
    remaining.map((asset) => asset.id),
    [protectedAsset.id],
  );
  for (const asset of original.slice(1, -1)) {
    assert.ok((await api(`assets/${asset.id}`)).deleted_at);
  }
  assert.equal((await api("assets?project=other")).length, 1);

  // Both dictionaries cover the selection controls and confirmation.
  await page.locator(".language").click();
  await button("Удалить выбранные (1)").click();
  await button("Точно удалить (1)?").waitFor();
  await page.keyboard.press("Escape");
  await button("Удалить выбранные (0)").waitFor();
  assert.equal(await button("Удалить выбранные (0)").isDisabled(), true);
  assert.deepEqual(errors, []);
  console.log(
    "PASS bulk selection, filters/project, confirmation, four workers, partial failure, translations",
  );
} finally {
  // The held job is running by now and cannot be cancelled; the isolated studio stops with the test.
  if (job)
    await fetch(`${base}/api/jobs/${job.id}/cancel`, { method: "POST" }).catch(
      () => {},
    );
  await browser.close();
}
