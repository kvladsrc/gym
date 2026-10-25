// Read-only browser regression: a shared FBX/JSON format must not mix workflows.
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
const errors = [];
page.on("pageerror", (error) => errors.push(error.message));
const task = (name, mime, prompt = "none") => ({
  task: name,
  output_mime: [mime],
  prompt,
  params_schema: { type: "object", properties: {} },
  inputs: [],
  max_count: 1,
});
const server = {
  id: "mixed",
  title: "Mixed model server",
  url: "http://127.0.0.1:9",
  state: "unavailable",
  seen: true,
  message: null,
  model: null,
  tasks: [
    task("image-to-3d", "model/gltf-binary"),
    task("3d-to-rig", "model/x-fbx"),
    task("prepare-rig", "model/x-fbx"),
    task("3d-paint", "model/gltf-binary"),
    task("text-to-motion", "model/x-fbx", "required"),
    task("video-to-motion", "model/x-fbx"),
    task("retarget-motion", "model/x-fbx"),
    task("retarget-native-motion", "model/x-fbx"),
    task("inbetween-native-motion", "text/plain"),
    task("refine-native-motion", "text/plain"),
    task("video-to-soma-motion", "text/plain"),
    task("video-to-pose-observations", "text/plain"),
    task("video-to-native-motion", "text/plain"),
    task("text-to-text", "text/plain", "required"),
  ],
};
const job = (taskName) => ({
  id: taskName,
  server: "retired-model",
  task: taskName,
  status: "cancelled",
  prompt: taskName,
  params: {},
  inputs: {},
  dependencies: {},
  outputs: [],
  count: 1,
  seed: 0,
  created_at: "2026-10-19T00:00:00Z",
  started_at: null,
  finished_at: null,
  deleted_at: null,
  timing: null,
  version: 1,
  error_code: null,
  error_message: null,
  model_snapshot: null,
});
const jobs = [
  "3d-to-rig",
  "prepare-rig",
  "text-to-motion",
  "retarget-native-motion",
  "inbetween-native-motion",
  "refine-native-motion",
  "video-to-soma-motion",
  "video-to-pose-observations",
  "video-to-native-motion",
  "text-to-text",
].map(job);
try {
  await page.route("**/api/**", async (route) => {
    const url = route.request().url();
    if (url.includes("/api/events"))
      return route.fulfill({
        contentType: "text/event-stream",
        body: ": connected\n\n",
      });
    if (url.includes("/api/servers")) return route.fulfill({ json: [server] });
    if (url.includes("/api/jobs")) return route.fulfill({ json: jobs });
    return route.continue();
  });
  await page.goto(`${base}/#/mesh`);
  const folder = (title) =>
    page
      .locator(".tree > ul > li")
      .filter({ has: page.locator(".tree-folder", { hasText: title }) });
  await folder("3D").waitFor();
  // A shared FBX/JSON format does not mix workflows: rigs stay in 3D, motion goes to Animations.
  assert.equal(await folder("3D").locator(".tree-task").count(), 4);
  assert.equal(await folder("Анимации").locator(".tree-task").count(), 9);
  assert.equal(await folder("Текст").locator(".tree-task").count(), 1);
  // The animation section's draft is its own, independent of 3D.
  await folder("Анимации").locator(".tree-model").first().click();
  await page.locator(".launch-head h1").waitFor();
  await page.locator("textarea").fill("A longsword guard and a clear strike");
  await folder("3D").locator(".tree-model").first().click();
  await page.waitForURL(/#\/mesh$/);
  await page.waitForFunction(() => !document.querySelector("textarea"));
  await folder("Анимации").locator(".tree-model").first().click();
  await page.waitForURL(/#\/animation$/);
  await page.locator("textarea").waitFor();
  assert.equal(
    await page.locator("textarea").inputValue(),
    "A longsword guard and a clear strike",
  );
  await page.locator(".language").click();
  assert.match(
    await folder("Animations").locator(".tree-folder").innerText(),
    /Animations/,
  );
  assert.equal(
    await page.evaluate(
      () => document.documentElement.scrollWidth > window.innerWidth,
    ),
    false,
  );
  assert.deepEqual(errors, []);
  console.log(
    "PASS 3D/Animations tasks in the tree, native JSON, drafts, translation",
  );
} finally {
  await browser.close();
}
