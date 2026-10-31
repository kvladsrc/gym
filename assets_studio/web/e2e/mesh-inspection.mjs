// Isolated Studio fixture: test the real model-viewer scene and renderer.
import assert from "node:assert/strict";
import { Buffer } from "node:buffer";
import { chromium } from "playwright-core";

const [base] = process.argv.slice(2);
function fixture() {
  const buffers = [
    new Float32Array([-1, -1, 0, 1, -1, 0, 1, 1, 0, -1, 1, 0]),
    new Uint16Array([0, 1, 2, 0, 2, 3]),
    new Float32Array([0, 0, 1, 0, 1, 1, 0, 1]),
    new Float32Array([-0.4, -0.4, 0.2, 0.4, -0.4, 0.2, 0, 0.4, 0.2]),
    new Float32Array([0, 0, 1, 0, 0.5, 1]),
  ].map((array) => Buffer.from(array.buffer));
  const binary = Buffer.concat(buffers);
  let offset = 0;
  const bufferViews = buffers.map((buffer) => {
    const view = { buffer: 0, byteOffset: offset, byteLength: buffer.length };
    offset += buffer.length;
    return view;
  });
  const png =
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGNoaPj/HwAGggL/s75RMwAAAABJRU5ErkJggg=="; // pragma: allowlist secret -- synthetic 1px PNG
  const gltf = {
    asset: { version: "2.0" },
    scene: 0,
    scenes: [{ nodes: [0, 1] }],
    nodes: [{ mesh: 0 }, { mesh: 1 }],
    buffers: [{ byteLength: binary.length }],
    bufferViews,
    accessors: [
      {
        bufferView: 0,
        componentType: 5126,
        count: 4,
        type: "VEC3",
        min: [-1, -1, 0],
        max: [1, 1, 0],
      },
      { bufferView: 1, componentType: 5123, count: 6, type: "SCALAR" },
      { bufferView: 2, componentType: 5126, count: 4, type: "VEC2" },
      {
        bufferView: 3,
        componentType: 5126,
        count: 3,
        type: "VEC3",
        min: [-0.4, -0.4, 0.2],
        max: [0.4, 0.4, 0.2],
      },
      { bufferView: 4, componentType: 5126, count: 3, type: "VEC2" },
    ],
    meshes: [
      {
        primitives: [
          {
            attributes: { POSITION: 0, TEXCOORD_0: 2 },
            indices: 1,
            material: 0,
          },
        ],
      },
      {
        primitives: [
          { attributes: { POSITION: 3, TEXCOORD_0: 4 }, material: 0 },
        ],
      },
    ],
    materials: [
      {
        pbrMetallicRoughness: {
          baseColorFactor: [0.8, 0.15, 0.05, 1],
          baseColorTexture: { index: 0 },
          metallicFactor: 0,
        },
        normalTexture: { index: 0 },
      },
    ],
    textures: [{ source: 0 }],
    images: [{ uri: `data:image/png;base64,${png}` }],
  };
  const json = Buffer.from(JSON.stringify(gltf));
  const padded = Buffer.concat([
    json,
    Buffer.alloc((4 - (json.length % 4)) % 4, 32),
  ]);
  const header = Buffer.alloc(20);
  header.writeUInt32LE(0x46546c67, 0);
  header.writeUInt32LE(2, 4);
  header.writeUInt32LE(28 + padded.length + binary.length, 8);
  header.writeUInt32LE(padded.length, 12);
  header.writeUInt32LE(0x4e4f534a, 16);
  const binHeader = Buffer.alloc(8);
  binHeader.writeUInt32LE(binary.length, 0);
  binHeader.writeUInt32LE(0x004e4942, 4);
  return Buffer.concat([header, padded, binHeader, binary]);
}
const browser = await chromium.launch({
  executablePath: process.env.CHROME ?? "google-chrome",
  args: ["--no-sandbox", "--use-gl=swiftshader", "--enable-unsafe-swiftshader"],
});
const page = await browser.newPage({
  viewport: { width: 1280, height: 800 },
  locale: "ru-RU",
});
const errors = [];
const preview = page.locator(".mesh-preview");
page.on("pageerror", (error) => errors.push(error.message));
async function state(save = false) {
  return page
    .locator(".mesh-preview model-viewer")
    .evaluate((element, save) => {
      const scene =
        element[
          Object.getOwnPropertySymbols(element).find(
            (key) => key.description === "scene",
          )
        ];
      const meshes = [];
      scene.model.traverse((node) => {
        if (node.isMesh) meshes.push(node);
      });
      const surfaces = meshes.filter((node) => !node.material.wireframe);
      if (save) window.meshOriginals = surfaces.map((node) => node.material);
      return {
        orbit: { ...element.getCameraOrbit() },
        wires: meshes.filter((node) => node.material.wireframe && node.visible)
          .length,
        restored: surfaces.every(
          (node, index) => node.material === window.meshOriginals[index],
        ),
        maps: surfaces.map((node) => [
          !!node.material.map,
          !!node.material.normalMap,
        ]),
      };
    }, save);
}
try {
  const uploaded = await fetch(
    `${base}/api/assets?title=mesh-inspection-fixture`,
    {
      method: "POST",
      headers: { "Content-Type": "application/octet-stream" },
      body: fixture(),
    },
  );
  assert.equal(uploaded.status, 201, await uploaded.clone().text());
  const asset = await uploaded.json();
  await page.goto(`${base}/#/library/${asset.id}`);
  await preview
    .getByRole("button", { name: "Геометрия", exact: true })
    .waitFor();
  await page.waitForFunction(() =>
    document.querySelector(".mesh-stats")?.textContent.includes("Вершин: 7"),
  );
  assert.match(
    await page.locator(".mesh-stats").innerText(),
    /Треугольников: 3/,
  );
  await page.waitForTimeout(700); // Let initial camera framing settle.
  const original = await state(true);
  assert.deepEqual(original.maps, [
    [true, true],
    [true, true],
  ]);
  await preview.getByRole("button", { name: "Геометрия", exact: true }).click();
  await page.waitForTimeout(100);
  const geometry = await state();
  assert.deepEqual(geometry.maps, [
    [false, false],
    [false, false],
  ]);
  assert.deepEqual(geometry.orbit, original.orbit);
  const clay = await page.locator(".mesh-preview model-viewer").screenshot();
  await preview.getByRole("button", { name: "Сетка", exact: true }).click();
  await page.waitForTimeout(100);
  assert.equal((await state()).wires, 2);
  const wire = await page.locator(".mesh-preview model-viewer").screenshot();
  assert.notDeepEqual(clay, wire, "wireframe must change rendered pixels");
  await page.getByLabel("Текстура", { exact: true }).check();
  await page.waitForTimeout(100);
  assert.deepEqual((await state()).maps, original.maps);
  await preview.getByRole("button", { name: "Материал", exact: true }).click();
  await page.waitForTimeout(100);
  const restored = await state();
  assert.equal(restored.wires, 0);
  assert.equal(restored.restored, true);
  assert.deepEqual(restored.orbit, original.orbit);
  // Reopening a cached asset must not retain inspection materials/overlays.
  await page.goto(`${base}/#/library`);
  await page.locator(".mesh-preview").waitFor({ state: "detached" });
  await page.goto(`${base}/#/library/${asset.id}`);
  await page.locator(".mesh-stats").waitFor();
  assert.equal((await state()).wires, 0);
  assert.deepEqual((await state()).maps, original.maps);
  await page.setViewportSize({ width: 760, height: 650 });
  await page.waitForTimeout(100);
  assert.equal(
    await page.evaluate(
      () => document.documentElement.scrollWidth > window.innerWidth,
    ),
    false,
  );
  const viewport = await page
    .locator(".mesh-preview model-viewer")
    .boundingBox();
  assert.ok(viewport.height > 100, "viewport must leave room for the model");
  assert.deepEqual(errors, []);
  console.log(
    "PASS indexed + non-indexed counts, wire pixels, texture/normal restoration, camera, cached reload, resizing",
  );
} finally {
  await browser.close();
}
