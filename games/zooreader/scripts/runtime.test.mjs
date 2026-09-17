import assert from "node:assert/strict";
import test from "node:test";
import { RenderQueue } from "../src/render-queue.js";
import { createPageWorld, terrainGroundAtX } from "../src/world.js";
import { clearKeys, isEditing, keys, isMoving } from "../src/input.js";
import { HeroSpeech } from "../src/speech.js";

const deferred = () => {
  let resolve, reject;
  const promise = new Promise((yes, no) => {
    resolve = yes;
    reject = no;
  });
  return { promise, resolve, reject };
};

test("world seeds always produce a known theme, flyers and valid patrol bounds", () => {
  for (const width of [320, 768, 1440]) {
    for (let page = 1; page <= 250; page++) {
      const info = { width, groundY: 690, gameHeight: 720 };
      const world = createPageWorld(page, info);
      assert.ok(["grass", "snow", "desert"].includes(world.terrainTheme));
      assert.ok(world.enemies.some((enemy) => enemy.type === "flyer"));
      assert.deepEqual(world, createPageWorld(page, info));
      for (const enemy of world.enemies.filter(
        (enemy) => enemy.type === "crawler",
      )) {
        assert.ok(enemy.x >= enemy.minX);
        assert.ok(enemy.x + enemy.width <= enemy.maxX);
      }
      for (let x = 0; x < width; x += 8) {
        assert.ok(
          Number.isFinite(terrainGroundAtX(world.terrain, x, info).groundY),
        );
      }
    }
  }
});

test("rendering is serial, skips intermediate requests and never commits stale output", async () => {
  const first = deferred();
  const started = [],
    committed = [];
  const queue = new RenderQueue(
    async (page) => {
      started.push(page);
      return page === 1 ? first.promise : page;
    },
    (result) => committed.push(result),
    assert.fail,
  );
  const idle = queue.request(1);
  queue.request(2);
  queue.request(3);
  assert.deepEqual(started, [1]);
  first.resolve(1);
  await idle;
  assert.deepEqual(started, [1, 3]);
  assert.deepEqual(committed, [3]);
  assert.equal(queue.busy, false);
});

test("a failed render releases the queue and a later page succeeds", async () => {
  const committed = [],
    errors = [];
  const queue = new RenderQueue(
    async (page) => {
      if (page === 1) throw new Error("broken page");
      return page;
    },
    (result) => committed.push(result),
    (error) => errors.push(error.message),
  );
  await queue.request(1);
  assert.equal(queue.busy, false);
  await queue.request(2);
  assert.deepEqual(errors, ["broken page"]);
  assert.deepEqual(committed, [2]);
});

test("replacing the document invalidates an in-progress render", async () => {
  const old = deferred();
  const committed = [];
  const queue = new RenderQueue(
    () => old.promise,
    (result) => committed.push(result),
    assert.fail,
  );
  const idle = queue.request("old");
  queue.invalidate();
  old.resolve("old page");
  await idle;
  assert.deepEqual(committed, []);
});

test("focus loss clears all movement keys; form controls keep native editing", () => {
  keys.KeyD = true;
  assert.equal(isMoving(), true);
  clearKeys();
  assert.equal(isMoving(), false);
  assert.equal(isEditing({ isContentEditable: true }), true);
  assert.equal(isEditing({ closest: () => ({ tagName: "INPUT" }) }), true);
  assert.equal(isEditing(null), false);
});

test("speech tolerates blocked storage and discards results from another page", async (t) => {
  t.mock.method(Math, "random", () => 0);
  const oldStorage = Object.getOwnPropertyDescriptor(
    globalThis,
    "sessionStorage",
  );
  Object.defineProperty(globalThis, "sessionStorage", {
    configurable: true,
    get() {
      throw new Error("blocked");
    },
  });
  t.after(() => {
    if (oldStorage)
      Object.defineProperty(globalThis, "sessionStorage", oldStorage);
    else delete globalThis.sessionStorage;
  });
  const result = deferred();
  t.mock.method(globalThis, "fetch", () => result.promise);
  const speech = new HeroSpeech({ value: "", addEventListener() {} });
  speech.keyInput.value = "test-key";
  speech.setPage(1);
  speech.maybeRequest(1, "A page about trees");
  speech.setPage(2);
  result.resolve({
    ok: true,
    json: async () => ({ output_text: "Old page quip" }),
  });
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(speech.currentText(), null);
  assert.equal(speech.inFlight, false);
  speech.resetDocument();
  assert.equal(speech.requestedPages.size, 0);
});
