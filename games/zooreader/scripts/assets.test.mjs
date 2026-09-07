#!/usr/bin/env node
import assert from "node:assert/strict";
import { execFileSync, spawn } from "node:child_process";
import {
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { createServer } from "node:http";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));
const ASSET_SCRIPT = resolve(SCRIPT_DIR, "assets.mjs");
const PLAYER_FILES = [
  "player.png",
  "player-run.png",
  "player-jump.png",
  "player-jetpack.png",
  "player-grenade.png",
  "player-flamethrower.png",
  "player-pistol.png",
  "player-knife.png",
];

function fixturePng(width = 1024, height = 1024) {
  const args = ["-size", `${width}x${height}`, "xc:#ff00ff"];
  const columns = width === 2048 ? 4 : 1;
  const columnWidth = width / columns;
  const runLeans = [-42, -14, 14, 42];
  for (let index = 0; index < columns; index += 1) {
    const center = index * columnWidth + columnWidth / 2;
    const lean = columns === 1 ? 0 : runLeans[index];
    args.push(
      "-fill",
      "black",
      "-draw",
      `roundrectangle ${center - 150 + lean},96 ${center + 150 + lean},920 40,40`,
      "-fill",
      "white",
      "-draw",
      `rectangle ${center - 82 + lean},180 ${center + 82 + lean},300`,
      ...(columns === 1
        ? []
        : [
            "-draw",
            `rectangle ${center - 128 + index * 34 + lean},520 ${center - 92 + index * 34 + lean},590`,
          ]),
    );
  }
  args.push("PNG32:-");
  return execFileSync("magick", args);
}

function prepareApp(t) {
  const app = mkdtempSync(join(tmpdir(), "zooreader-assets-test-"));
  mkdirSync(join(app, "assets"), { recursive: true });
  t.after(() => rmSync(app, { recursive: true, force: true }));
  return app;
}

async function startMockApi(t, responder = () => ({})) {
  const staticPng = fixturePng().toString("base64");
  const runPng = fixturePng(2048, 1024).toString("base64");
  const requests = [];
  const counts = { generations: 0, edits: 0 };
  const server = createServer((request, response) => {
    const chunks = [];
    request.on("data", (chunk) => chunks.push(chunk));
    request.on("end", () => {
      const kind = request.url?.endsWith("/generations")
        ? "generations"
        : request.url?.endsWith("/edits")
          ? "edits"
          : "unknown";
      if (kind !== "unknown") counts[kind] += 1;
      const body = Buffer.concat(chunks);
      requests.push({
        kind,
        headers: request.headers,
        body,
      });

      const decision = responder({
        kind,
        index: counts[kind] ?? 0,
        request,
        body,
      });
      const status = decision.status ?? 200;
      response.writeHead(status, {
        "content-type": "application/json",
        ...(decision.retryAfter === undefined
          ? {}
          : { "retry-after": String(decision.retryAfter) }),
      });
      if (status >= 400) {
        response.end(
          JSON.stringify({
            error: { message: decision.message ?? "mock failure" },
          }),
        );
        return;
      }
      const encodedPng = body.includes(Buffer.from("2048x1024"))
        ? runPng
        : staticPng;
      response.end(
        JSON.stringify({
          data: [{ b64_json: encodedPng }],
          usage: { total_tokens: 123 },
        }),
      );
    });
  });

  await new Promise((resolvePromise, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", resolvePromise);
  });
  t.after(
    () =>
      new Promise((resolvePromise) => {
        server.close(resolvePromise);
      }),
  );
  const address = server.address();
  assert.equal(typeof address, "object");
  return {
    counts,
    requests,
    url: `http://127.0.0.1:${address.port}/v1/images`,
  };
}

function runAssetCommand(app, apiUrl, command, options = {}) {
  const environment = {
    ...process.env,
    ZOOREADER_ASSET_APP_DIR: app,
    OPENAI_IMAGES_API_URL: apiUrl,
    OPENAI_IMAGE_MODEL: "gpt-image-2",
    OPENAI_IMAGE_QUALITY: "high",
    OPENAI_IMAGE_REQUEST_ATTEMPTS: String(options.attempts ?? 2),
  };
  if (options.key === null) {
    delete environment.OPENAI_API_KEY;
  } else {
    environment.OPENAI_API_KEY = options.key ?? "test-api-key";
  }

  return new Promise((resolvePromise, reject) => {
    const child = spawn(process.execPath, [ASSET_SCRIPT, command], {
      cwd: app,
      env: environment,
      stdio: ["ignore", "pipe", "pipe"],
    });
    let stdout = "";
    let stderr = "";
    child.stdout.setEncoding("utf8");
    child.stderr.setEncoding("utf8");
    child.stdout.on("data", (chunk) => {
      stdout += chunk;
    });
    child.stderr.on("data", (chunk) => {
      stderr += chunk;
    });
    child.once("error", reject);
    child.once("close", (code) => {
      resolvePromise({ code, stdout, stderr });
    });
  });
}

function pngSize(path) {
  const bytes = readFileSync(path);
  return {
    width: bytes.readUInt32BE(16),
    height: bytes.readUInt32BE(20),
  };
}

function activeConcept(app) {
  return JSON.parse(
    readFileSync(join(app, "asset-work/current-hero.json"), "utf8"),
  ).concept;
}

test("concept plus full creates static poses and a four-frame run cycle", async (t) => {
  const app = prepareApp(t);
  const api = await startMockApi(t);

  const conceptResult = await runAssetCommand(app, api.url, "hero-concept");
  assert.equal(conceptResult.code, 0, conceptResult.stderr);
  assert.equal(api.counts.generations, 1);
  assert.equal(api.counts.edits, 0);
  assert.deepEqual(readdirSync(join(app, "assets")), []);

  const concept = activeConcept(app);
  assert.ok(
    existsSync(join(app, "asset-work/concepts", concept, "concept.png")),
  );

  const fullResult = await runAssetCommand(app, api.url, "hero-full");
  assert.equal(fullResult.code, 0, fullResult.stderr);
  assert.equal(api.counts.generations, 1);
  assert.equal(api.counts.edits, 7);

  for (const file of PLAYER_FILES) {
    const path = join(app, "assets", file);
    assert.ok(existsSync(path), `${file} was not installed`);
    assert.deepEqual(
      pngSize(path),
      file === "player-run.png"
        ? { width: 384, height: 96 }
        : { width: 96, height: 96 },
    );
  }
  assert.ok(
    existsSync(join(app, "asset-work/heroes", concept, "contact-sheet.png")),
  );
  assert.ok(
    existsSync(join(app, "asset-work/heroes", concept, "run-preview.gif")),
  );

  for (const request of api.requests) {
    assert.equal(request.headers.authorization, "Bearer test-api-key");
  }
  const editRequests = api.requests.filter(({ kind }) => kind === "edits");
  for (const request of editRequests) {
    const body = request.body.toString("latin1");
    assert.match(body, /name="image\[\]"/);
    assert.match(body, /filename="hero-reference\.png"/);
    assert.match(body, /name="model"\r\n\r\ngpt-image-2/);
    assert.match(body, /name="quality"\r\n\r\nhigh/);
  }
  const runRequests = editRequests.filter(({ body }) =>
    body.includes(Buffer.from("2048x1024")),
  );
  assert.equal(runRequests.length, 1);
  assert.match(
    runRequests[0].body.toString("latin1"),
    /four-frame running loop/,
  );
  const staticRequests = editRequests.filter(
    ({ body }) => !body.includes(Buffer.from("2048x1024")),
  );
  assert.equal(staticRequests.length, 6);
  for (const request of staticRequests) {
    const body = request.body.toString("latin1");
    assert.match(body, /name="size"\r\n\r\n1024x1024/);
    assert.match(body, /an animation, sprite sheet, sequence/);
  }

  const manifest = readFileSync(
    join(app, "asset-work/heroes", concept, "full-manifest.json"),
    "utf8",
  );
  assert.doesNotMatch(manifest, /test-api-key/);

  const resumed = await runAssetCommand(app, api.url, "hero-full", {
    key: null,
  });
  assert.equal(resumed.code, 0, resumed.stderr);
  assert.equal(api.counts.generations, 1);
  assert.equal(api.counts.edits, 7);
});

test("a failed full run leaves all runtime assets unchanged", async (t) => {
  const app = prepareApp(t);
  const api = await startMockApi(t, ({ kind, index }) =>
    kind === "edits" && index === 3
      ? { status: 400, message: "bad pose request" }
      : {},
  );
  const conceptResult = await runAssetCommand(app, api.url, "hero-concept");
  assert.equal(conceptResult.code, 0, conceptResult.stderr);

  const sentinel = Buffer.from("existing-runtime-asset");
  for (const file of PLAYER_FILES) {
    writeFileSync(join(app, "assets", file), sentinel);
  }

  const fullResult = await runAssetCommand(app, api.url, "hero-full");
  assert.notEqual(fullResult.code, 0);
  assert.match(fullResult.stderr, /bad pose request/);
  assert.equal(api.counts.edits, 3);
  for (const file of PLAYER_FILES) {
    assert.deepEqual(readFileSync(join(app, "assets", file)), sentinel);
  }
});

test("a missing API key fails before creating asset-work", async (t) => {
  const app = prepareApp(t);
  const result = await runAssetCommand(
    app,
    "http://127.0.0.1:1/v1/images",
    "hero-concept",
    { key: null },
  );
  assert.notEqual(result.code, 0);
  assert.match(result.stderr, /OPENAI_API_KEY is required/);
  assert.equal(existsSync(join(app, "asset-work")), false);
});

test("429 retries but an authentication error does not", async (t) => {
  const retryApp = prepareApp(t);
  const retryApi = await startMockApi(t, ({ kind, index }) =>
    kind === "generations" && index === 1 ? { status: 429, retryAfter: 0 } : {},
  );
  const retried = await runAssetCommand(retryApp, retryApi.url, "hero-concept");
  assert.equal(retried.code, 0, retried.stderr);
  assert.equal(retryApi.counts.generations, 2);

  const authApp = prepareApp(t);
  const authApi = await startMockApi(t, () => ({
    status: 401,
    message: "invalid key",
  }));
  const rejected = await runAssetCommand(authApp, authApi.url, "hero-concept", {
    attempts: 4,
  });
  assert.notEqual(rejected.code, 0);
  assert.match(rejected.stderr, /invalid key/);
  assert.equal(authApi.counts.generations, 1);
});
