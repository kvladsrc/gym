#!/usr/bin/env node
// Real-browser checks using Chrome DevTools Protocol; no npm dependencies.
import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { mkdtemp, readFile, writeFile, rm } from "node:fs/promises";
import { createServer } from "node:http";
import { tmpdir } from "node:os";
import { join, resolve, extname } from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("../", import.meta.url));
const work = await mkdtemp(join(tmpdir(), "zooreader-smoke-"));
const server = createServer(async (req, res) => {
  let path = resolve(root, "." + new URL(req.url, "http://localhost").pathname);
  if (path === root.slice(0, -1)) path = join(path, "index.html");
  if (!path.startsWith(root)) {
    res.writeHead(403).end();
    return;
  }
  try {
    const body = await readFile(
      path.endsWith("/") || path === root.slice(0, -1)
        ? join(path, "index.html")
        : path,
    );
    res.setHeader(
      "Content-Type",
      {
        ".html": "text/html",
        ".js": "text/javascript",
        ".mjs": "text/javascript",
        ".css": "text/css",
        ".wasm": "application/wasm",
      }[extname(path)] || "application/octet-stream",
    );
    res.end(body);
  } catch {
    res.writeHead(404).end();
  }
});
await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
const origin = `http://127.0.0.1:${server.address().port}`;
const chrome = spawn(
  process.env.CHROME_BIN || "google-chrome",
  [
    "--headless=new",
    "--no-sandbox",
    "--disable-dev-shm-usage",
    "--disable-gpu",
    "--remote-debugging-port=0",
    "--remote-debugging-address=127.0.0.1",
    `--user-data-dir=${work}/chrome`,
    "about:blank",
  ],
  { stdio: ["ignore", "ignore", "pipe"] },
);
let socket;
const timeout = setTimeout(() => {
  console.error("Browser smoke timed out");
  chrome.kill();
}, 60000);

try {
  const wsUrl = await new Promise((resolve, reject) => {
    let output = "";
    chrome.on("error", reject);
    chrome.on("exit", (code) =>
      reject(new Error(`Chrome exited: ${code}\n${output}`)),
    );
    chrome.stderr.on("data", (chunk) => {
      output += chunk;
      const match = output.match(/DevTools listening on (ws:\/\/[^\s]+)/);
      if (match) resolve(match[1]);
    });
  });
  const targets = await (
    await fetch(`http://${new URL(wsUrl).host}/json/list`)
  ).json();
  socket = new WebSocket(
    targets.find((target) => target.type === "page").webSocketDebuggerUrl,
  );
  await new Promise((resolve, reject) => {
    socket.onopen = resolve;
    socket.onerror = reject;
  });
  let id = 0;
  const pending = new Map(),
    exceptions = [],
    external = [],
    logs = [];
  socket.onmessage = ({ data }) => {
    const message = JSON.parse(data);
    if (message.id) {
      const request = pending.get(message.id);
      pending.delete(message.id);
      if (message.error) request.reject(new Error(message.error.message));
      else request.resolve(message.result);
    } else if (message.method === "Runtime.consoleAPICalled")
      logs.push(message.params.args.map((arg) => arg.description || arg.value));
    else if (message.method === "Runtime.exceptionThrown")
      exceptions.push(message.params.exceptionDetails);
    else if (
      message.method === "Network.requestWillBeSent" &&
      /^https?:/.test(message.params.request.url) &&
      !message.params.request.url.startsWith(origin)
    )
      external.push(message.params.request.url);
  };
  const call = (method, params = {}) =>
    new Promise((resolve, reject) => {
      pending.set(++id, { resolve, reject });
      socket.send(JSON.stringify({ id, method, params }));
    });
  const evaluate = async (expression) => {
    const result = await call("Runtime.evaluate", {
      expression,
      awaitPromise: true,
      returnByValue: true,
    });
    if (result.exceptionDetails)
      throw new Error(JSON.stringify(result.exceptionDetails));
    return result.result.value;
  };
  const waitFor = async (expression) => {
    for (let attempt = 0; attempt < 100; attempt++) {
      if (await evaluate(expression)) return;
      await new Promise((resolve) => setTimeout(resolve, 50));
    }
    throw new Error(
      `Timed out: ${expression}\n${JSON.stringify(logs)}\n${await evaluate("document.body.innerText")}`,
    );
  };
  await call("Runtime.enable");
  await call("Network.enable");
  await call("Page.enable");
  await call("Emulation.setDeviceMetricsOverride", {
    width: 1100,
    height: 760,
    deviceScaleFactor: 1,
    mobile: false,
  });
  await call("Page.navigate", { url: origin + "/" });
  await waitFor("document.querySelector('#pdf-upload') !== null");
  // Wait for module initialization, not just HTML parsing.
  await waitFor("document.querySelector('#game-canvas').width === 1100");

  function pdf(pages) {
    const objects = [
      "<< /Type /Catalog /Pages 2 0 R >>",
      `<< /Type /Pages /Count ${pages} /Kids [${Array.from({ length: pages }, (_, i) => `${4 + i * 2} 0 R`).join(" ")}] >>`,
      "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
    ];
    for (let page = 1; page <= pages; page++) {
      const lines = Array.from(
        { length: 24 },
        (_, i) =>
          `BT /F1 14 Tf 55 ${760 - i * 28} Td (Zooreader - page ${page} - reading line ${i + 1}) Tj ET`,
      ).join("\n");
      objects.push(
        `<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents ${5 + (page - 1) * 2} 0 R >>`,
        `<< /Length ${lines.length} >>\nstream\n${lines}\nendstream`,
      );
    }
    let result = "%PDF-1.4\n";
    const offsets = [0];
    objects.forEach((body, i) => {
      offsets.push(result.length);
      result += `${i + 1} 0 obj\n${body}\nendobj\n`;
    });
    const xref = result.length;
    result += `xref\n0 ${offsets.length}\n0000000000 65535 f \n`;
    result += offsets
      .slice(1)
      .map((offset) => `${String(offset).padStart(10, "0")} 00000 n \n`)
      .join("");
    return (
      result +
      `trailer\n<< /Size ${offsets.length} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`
    );
  }
  const upload = async (path) => {
    const { root } = await call("DOM.getDocument");
    const { nodeId } = await call("DOM.querySelector", {
      nodeId: root.nodeId,
      selector: "#pdf-upload",
    });
    await call("DOM.setFileInputFiles", { nodeId, files: [path] });
  };
  const sample = join(work, "sample.pdf");
  await writeFile(sample, pdf(4));
  await upload(sample);
  await waitFor(
    "document.querySelector('#page-count').textContent === '4' && document.querySelector('#loading').classList.contains('hidden')",
  );
  await evaluate(
    "document.querySelector('#next-page').click(); document.querySelector('#next-page').click(); document.querySelector('#next-page').click()",
  );
  await waitFor("document.querySelector('#page-num').value === '4'");
  await evaluate("document.querySelector('#prev-page').click()");
  await waitFor("document.querySelector('#page-num').value === '3'");
  await evaluate("document.querySelector('#zoom-in').click()");
  await waitFor("document.querySelector('#zoom-value').textContent === '110%'");
  assert.equal(
    await evaluate(
      `(() => { const input = document.querySelector('#openai-key'); const event = new KeyboardEvent('keydown', {code: 'KeyD', bubbles: true, cancelable: true}); return input.dispatchEvent(event); })()`,
    ),
    true,
  );
  await call("Emulation.setDeviceMetricsOverride", {
    width: 640,
    height: 700,
    deviceScaleFactor: 2,
    mobile: false,
  });
  await waitFor("document.querySelector('#game-canvas').width === 1280");
  await new Promise((resolve) => setTimeout(resolve, 700));
  assert.equal(
    await evaluate("document.querySelector('#page-num').value"),
    "3",
  );
  const bad = join(work, "bad.pdf");
  await writeFile(bad, "not a PDF");
  await upload(bad);
  await waitFor(
    "document.querySelector('#loading').textContent.includes('Could not')",
  );
  await upload(sample);
  await waitFor(
    "document.querySelector('#page-num').value === '1' && document.querySelector('#loading').classList.contains('hidden')",
  );
  await new Promise((resolve) => setTimeout(resolve, 500));
  await evaluate("document.activeElement.blur()");
  const beforeMovement = await evaluate(
    "document.querySelector('#bg-canvas').toDataURL()",
  );
  await call("Input.dispatchKeyEvent", {
    type: "keyDown",
    code: "KeyD",
    key: "d",
    windowsVirtualKeyCode: 68,
  });
  await new Promise((resolve) => setTimeout(resolve, 250));
  await call("Input.dispatchKeyEvent", {
    type: "keyUp",
    code: "KeyD",
    key: "d",
    windowsVirtualKeyCode: 68,
  });
  await new Promise((resolve) => setTimeout(resolve, 200));
  assert.notEqual(
    await evaluate("document.querySelector('#bg-canvas').toDataURL()"),
    beforeMovement,
  );
  await evaluate(
    "window.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyD' })); window.dispatchEvent(new Event('blur'))",
  );
  assert.equal(
    await evaluate(
      "import('./src/input.js').then((input) => input.isMoving())",
    ),
    false,
  );
  const screenshot = process.env.SCREENSHOT || join(work, "preview.png");
  await writeFile(
    screenshot,
    Buffer.from((await call("Page.captureScreenshot")).data, "base64"),
  );
  assert.deepEqual(exceptions, []);
  assert.deepEqual(external, []);
  console.log(
    `Browser smoke passed: PDF, rapid navigation, zoom, resize/DPR, input, invalid PDF recovery, no runtime CDN requests. Screenshot: ${screenshot}`,
  );
} finally {
  clearTimeout(timeout);
  socket?.close();
  const stopped = new Promise((resolve) => chrome.once("exit", resolve));
  if (chrome.exitCode === null) {
    chrome.kill();
    await stopped;
  }
  server.close();
  await rm(join(work, "chrome"), {
    recursive: true,
    force: true,
    maxRetries: 3,
  });
}
