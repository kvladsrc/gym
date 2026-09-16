import { createRenderer } from "./renderer.js";
import { loadSprites } from "./assets.js";
import { maybeFireAtTarget, updateProjectiles } from "./combat.js";
import { LAYOUT, PHYSICS } from "./config.js";
import {
  addImpactEffect,
  clearExpiredEffects,
  updateEffectAges,
} from "./effects.js";
import {
  isMoving,
  isMovingLeft,
  isMovingRight,
  keys,
  clearKeys,
  isEditing,
} from "./input.js";
import { RenderQueue } from "./render-queue.js";
import * as pdfjsLib from "../vendor/pdfjs/build/pdf.min.mjs";
import { clamp, intersects } from "./math.js";
import { HeroSpeech } from "./speech.js";
import { createPageWorld, terrainGroundAtX } from "./world.js";

// PDF.js settings
pdfjsLib.GlobalWorkerOptions.workerSrc = new URL(
  "../vendor/pdfjs/build/pdf.worker.min.mjs",
  import.meta.url,
).href;

// ---------------------------------------------------------------------------
// DOM Elements
// ---------------------------------------------------------------------------

const uploadInput = document.getElementById("pdf-upload");
const uiContainer = document.getElementById("ui-container");
const controls = document.getElementById("controls");
const loading = document.getElementById("loading");
const pageNumInput = document.getElementById("page-num");
const pageCountSpan = document.getElementById("page-count");
const prevPageButton = document.getElementById("prev-page");
const nextPageButton = document.getElementById("next-page");
const zoomOutButton = document.getElementById("zoom-out");
const zoomInButton = document.getElementById("zoom-in");
const zoomValue = document.getElementById("zoom-value");
const openaiKeyInput = document.getElementById("openai-key");

// Canvas Elements
const bgCanvas = document.getElementById("bg-canvas");
const gameCanvas = document.getElementById("game-canvas");
const bgCtx = bgCanvas.getContext("2d");
const gameCtx = gameCanvas.getContext("2d");

let offscreenCanvas = document.createElement("canvas");

// ---------------------------------------------------------------------------
// State
// ---------------------------------------------------------------------------

let pdfDoc = null;
let pdfTask = null;
let pageNum = 1;
let requestedPage = 1;
let requestedProgress = 0;
let uploadVersion = 0;
let uploading = false;
let renderedDocument = null;
let readerTop = 0;
let renderedDpr = window.devicePixelRatio || 1;
let gameLoopId = null;
let scrollY = 0;
let lastScrollY = -1;
let bgNeedsRedraw = true;
let resizeTimeout = null;
let pdfZoom = 1;
let terrain = [];
let terrainTheme = "grass";
let enemies = [];
let impactEffects = [];
let projectiles = [];
let animationFrame = 0;
let wheelTurboVelocity = 0;

// ---------------------------------------------------------------------------
// Sprites
// ---------------------------------------------------------------------------

const sprites = loadSprites();
const heroSpeech = new HeroSpeech(openaiKeyInput);

// ---------------------------------------------------------------------------
// Game objects
// ---------------------------------------------------------------------------

const gameInfo = {
  width: window.innerWidth,
  bookHeight: window.innerHeight,
  gameHeight: window.innerHeight,
  groundY: window.innerHeight - LAYOUT.GROUND_OFFSET,
};

const player = {
  x: 0,
  y: gameInfo.groundY - LAYOUT.PLAYER_SIZE,
  width: LAYOUT.PLAYER_SIZE,
  height: LAYOUT.PLAYER_SIZE,
  vx: 0,
  vy: 0,
  speed: 8,
  gravity: 0.8,
  isGrounded: true,
  facingRight: true,
  attackTimer: 0,
  fireCooldown: 0,
  weaponTimer: 0,
  weaponState: "idle",
  jetpackActive: false,
  turboJetpackActive: false,
};

// ---------------------------------------------------------------------------
// Resize
// ---------------------------------------------------------------------------

function resize() {
  const progress = player.x / Math.max(1, gameInfo.width - player.width);
  gameInfo.width = window.innerWidth;
  gameInfo.bookHeight = window.innerHeight;
  gameInfo.gameHeight = window.innerHeight;
  gameInfo.groundY = window.innerHeight - LAYOUT.GROUND_OFFSET;

  const dpr = window.devicePixelRatio || 1;

  bgCanvas.width = gameInfo.width * dpr;
  bgCanvas.height = gameInfo.bookHeight * dpr;

  gameCanvas.width = gameInfo.width * dpr;
  gameCanvas.height = gameInfo.gameHeight * dpr;

  bgCtx.setTransform(dpr, 0, 0, dpr, 0, 0);
  gameCtx.setTransform(dpr, 0, 0, dpr, 0, 0);

  gameCtx.imageSmoothingEnabled = false;
  player.x = clamp(progress, 0, 1) * Math.max(0, gameInfo.width - player.width);
  bgNeedsRedraw = true;

  if (pdfDoc) {
    clearTimeout(resizeTimeout);
    resizeTimeout = setTimeout(() => {
      queueRenderPage(requestedPage);
    }, LAYOUT.RESIZE_DEBOUNCE_MS);
  }
}

window.addEventListener("resize", resize);
resize();

// ---------------------------------------------------------------------------
// Input listeners
// ---------------------------------------------------------------------------

uploadInput.addEventListener("change", handleUpload);

pageNumInput.addEventListener("change", (e) => {
  const num = parseInt(e.target.value, 10);
  if (pdfDoc && num > 0 && num <= pdfDoc.numPages) {
    queueRenderPage(num);
  } else {
    pageNumInput.value = pageNum;
  }
});

prevPageButton.addEventListener("click", onPrevPage);
nextPageButton.addEventListener("click", onNextPage);

zoomOutButton.addEventListener("click", () =>
  setPdfZoom(pdfZoom - LAYOUT.PDF_ZOOM_STEP),
);
zoomInButton.addEventListener("click", () =>
  setPdfZoom(pdfZoom + LAYOUT.PDF_ZOOM_STEP),
);

window.addEventListener("keydown", (e) => {
  if (
    e.code in keys &&
    !isEditing(e.target) &&
    !e.ctrlKey &&
    !e.metaKey &&
    !e.altKey
  ) {
    keys[e.code] = true;
    e.preventDefault();
  }
});

window.addEventListener(
  "keyup",
  (e) => {
    if (e.code in keys) {
      keys[e.code] = false;
    }
  },
  { passive: true },
);

window.addEventListener("blur", () => {
  clearKeys();
  wheelTurboVelocity = 0;
  player.vx = 0;
});
window.addEventListener("focusin", (e) => {
  if (isEditing(e.target)) clearKeys();
});
document.addEventListener("visibilitychange", () => {
  if (document.hidden) {
    clearKeys();
    wheelTurboVelocity = 0;
    player.vx = 0;
  }
});

window.addEventListener(
  "wheel",
  (e) => {
    if (!pdfDoc || isEditing(e.target) || e.ctrlKey || e.metaKey) return;
    e.preventDefault();
    wheelTurboVelocity = clamp(
      wheelTurboVelocity +
        e.deltaY *
          (e.deltaMode === 1
            ? 16
            : e.deltaMode === 2
              ? gameInfo.bookHeight
              : 1) *
          LAYOUT.WHEEL_TURBO_SCALE,
      -player.speed * 3,
      player.speed * 3,
    );
  },
  { passive: false },
);

// ---------------------------------------------------------------------------
// PDF handling
// ---------------------------------------------------------------------------

/**
 * Handle PDF file upload: parse and start the game loop.
 * @param {Event} e — change event from the file input
 */
async function handleUpload(e) {
  const file = e.target.files[0];
  if (!file) return;
  const version = ++uploadVersion;
  uploading = true;
  clearKeys();
  wheelTurboVelocity = 0;
  loading.textContent = "Opening PDF…";
  loading.classList.remove("hidden");
  let candidate = null;
  let candidateTask = null;
  try {
    const data = await file.arrayBuffer();
    if (version !== uploadVersion) return;
    const base = new URL("../vendor/pdfjs/", import.meta.url).href;
    candidateTask = pdfjsLib.getDocument({
      data,
      isEvalSupported: false,
      cMapUrl: base + "cmaps/",
      cMapPacked: true,
      standardFontDataUrl: base + "standard_fonts/",
      wasmUrl: base + "wasm/",
      iccUrl: base + "iccs/",
    });
    candidate = await candidateTask.promise;
    if (version !== uploadVersion) {
      await candidateTask.destroy();
      return;
    }
    const previous = pdfTask;
    renderQueue.invalidate();
    pdfDoc = candidate;
    pdfTask = candidateTask;
    heroSpeech.resetDocument();
    pageCountSpan.textContent = pdfDoc.numPages;
    pageNumInput.max = pdfDoc.numPages;
    requestedPage = 1;
    player.vx = 0;
    await queueRenderPage(1, 0);
    if (previous) await previous.destroy();
  } catch (err) {
    if (candidateTask && candidateTask !== pdfTask)
      await candidateTask.destroy();
    if (version === uploadVersion) showPdfError(err);
  } finally {
    if (version === uploadVersion) {
      uploading = false;
      uploadInput.value = "";
      if (document.activeElement === uploadInput) uploadInput.blur();
    }
  }
}

function showPdfError(error) {
  console.error("Error rendering PDF:", error);
  loading.textContent = "Could not open this page. Try another page or PDF.";
  loading.classList.remove("hidden");
  requestedPage = pageNum;
  pageNumInput.value = pageNum;
  updatePageControls();
}

// Render into a fresh canvas; an old page remains readable until commit.
const renderQueue = new RenderQueue(
  async (view) => {
    const page = await view.document.getPage(view.num);
    const viewport = page.getViewport({ scale: 1 });
    const scale = (view.width / viewport.width) * view.dpr * view.zoom;
    const scaledViewport = page.getViewport({ scale });
    const canvas = document.createElement("canvas");
    canvas.width = Math.ceil(scaledViewport.width);
    canvas.height = Math.ceil(scaledViewport.height);
    await page.render({
      canvasContext: canvas.getContext("2d"),
      viewport: scaledViewport,
    }).promise;
    let text = "";
    // Text extraction is optional and must never make a readable PDF fail.
    if (heroSpeech.enabled()) {
      try {
        const content = await page.getTextContent();
        text = content.items.map((item) => item.str || "").join(" ");
      } catch (error) {
        console.warn("Could not extract page text", error);
      }
    }
    return { canvas, text };
  },
  ({ canvas, text }, view) => {
    const changedPage =
      renderedDocument !== view.document || pageNum !== view.num;
    offscreenCanvas = canvas;
    renderedDocument = view.document;
    renderedDpr = view.dpr;
    pageNum = view.num;
    pageNumInput.value = pageNum;
    player.x = view.progress * Math.max(0, gameInfo.width - player.width);
    bgNeedsRedraw = true;
    controls.classList.remove("hidden");
    loading.classList.add("hidden");
    uiContainer.classList.add("minimized");
    updatePageControls();
    if (
      changedPage ||
      view.width !== worldWidth ||
      gameInfo.groundY !== worldGroundY
    )
      generateWorld();
    heroSpeech.setPage(pageNum);
    heroSpeech.maybeRequest(pageNum, text);
    if (!gameLoopId) gameLoopId = requestAnimationFrame(gameLoop);
  },
  showPdfError,
);
let worldWidth = 0;
let worldGroundY = 0;

/** @param {number} nextZoom */
function setPdfZoom(nextZoom) {
  pdfZoom = clamp(nextZoom, LAYOUT.PDF_ZOOM_MIN, LAYOUT.PDF_ZOOM_MAX);
  zoomValue.textContent = `${Math.round(pdfZoom * 100)}%`;
  bgNeedsRedraw = true;
  if (pdfDoc) {
    queueRenderPage(requestedPage);
  }
}

function updatePageControls() {
  if (!pdfDoc) return;

  prevPageButton.disabled = requestedPage <= 1;
  nextPageButton.disabled = requestedPage >= pdfDoc.numPages;
}

/**
 * Generate deterministic world objects for the current page.
 * Seed is based on pageNum so terrain and actors stay consistent.
 */
function generateWorld() {
  worldWidth = gameInfo.width;
  worldGroundY = gameInfo.groundY;
  const world = createPageWorld(pageNum, gameInfo);
  terrain = world.terrain;
  terrainTheme = world.terrainTheme;
  enemies = world.enemies;
  impactEffects = [];
  projectiles = [];
}

/**
 * Queue a page render — defers if a render is already in progress.
 * @param {number} num — 1-indexed page number
 */
function queueRenderPage(num, progress) {
  if (!pdfDoc) return Promise.resolve();
  num = clamp(num, 1, pdfDoc.numPages);
  const samePage = num === requestedPage;
  requestedProgress =
    progress ??
    (samePage && renderQueue.busy
      ? requestedProgress
      : samePage
        ? clamp(player.x / Math.max(1, gameInfo.width - player.width), 0, 1)
        : 0);
  requestedPage = num;
  updatePageControls();
  return renderQueue.request({
    document: pdfDoc,
    num,
    width: gameInfo.width,
    dpr: window.devicePixelRatio || 1,
    zoom: pdfZoom,
    progress: requestedProgress,
  });
}

function onPrevPage() {
  if (!pdfDoc || requestedPage <= 1) return;
  queueRenderPage(requestedPage - 1, 1);
}

function onNextPage() {
  if (!pdfDoc || requestedPage >= pdfDoc.numPages) return;
  queueRenderPage(requestedPage + 1, 0);
}

// ---------------------------------------------------------------------------
// Game engine — update (delta-time normalised to ~60 fps)
// ---------------------------------------------------------------------------

/**
 * Update game state. All physics values are normalised so dt ≈ 1.0 at 60 fps.
 * @param {number} dt — frame delta normalised to 60 fps (1.0 = 16.667 ms)
 */
function update(dt) {
  if (renderQueue.busy || uploading || !terrain.length) return;
  animationFrame += dt;
  const usingWheelTurbo =
    Math.abs(wheelTurboVelocity) > LAYOUT.WHEEL_TURBO_MIN_SPEED;

  // Horizontal movement
  if (usingWheelTurbo) {
    player.vx = wheelTurboVelocity;
    player.facingRight = wheelTurboVelocity > 0;
    wheelTurboVelocity *= Math.pow(LAYOUT.WHEEL_TURBO_DECAY, dt);
  } else if (isMovingRight()) {
    player.vx += PHYSICS.ACCELERATION * dt;
    player.facingRight = true;
  } else if (isMovingLeft()) {
    player.vx -= PHYSICS.ACCELERATION * dt;
    player.facingRight = false;
  } else {
    // Friction — applied per-frame; raise to dt power for frame-rate independence
    player.vx *= Math.pow(PHYSICS.FRICTION, dt);
  }

  // Cap speed
  player.vx = usingWheelTurbo
    ? clamp(player.vx, -player.speed * 3, player.speed * 3)
    : clamp(player.vx, -player.speed, player.speed);

  const idle = !isMoving() && !usingWheelTurbo;
  const currentTraversal = terrainGroundAtX(
    terrain,
    player.x + player.width / 2,
    gameInfo,
  );
  if (idle && currentTraversal.isGap) {
    player.vx = 0;
  }

  // Move horizontally
  player.x += player.vx * dt;

  // Page bounds
  if (player.x > gameInfo.width - player.width) {
    if (pageNum < pdfDoc.numPages) {
      onNextPage();
    } else {
      player.x = gameInfo.width - player.width;
      player.vx = 0;
    }
  } else if (player.x < 0) {
    if (pageNum > 1) {
      onPrevPage();
    } else {
      player.x = 0;
      player.vx = 0;
    }
  }

  const traversal = terrainGroundAtX(
    terrain,
    player.x + player.width / 2,
    gameInfo,
  );
  player.turboJetpackActive = usingWheelTurbo;
  player.jetpackActive = player.turboJetpackActive || (idle && traversal.isGap);
  player.y = player.turboJetpackActive
    ? gameInfo.groundY -
      LAYOUT.TERRAIN_LEVEL_HEIGHT -
      LAYOUT.WHEEL_TURBO_HEIGHT -
      player.height
    : traversal.groundY - player.height;
  player.vy = 0;
  player.isGrounded = !traversal.isGap;

  player.attackTimer = Math.max(0, player.attackTimer - dt);
  player.fireCooldown = Math.max(0, player.fireCooldown - dt);
  player.weaponTimer = Math.max(0, player.weaponTimer - dt);
  updateEnemies(dt);
  projectiles = maybeFireAtTarget(player, enemies, projectiles);

  projectiles = updateProjectiles(projectiles, dt);
  const remainingProjectiles = [];
  const hitEnemies = new Set();
  for (const projectile of projectiles) {
    const hitSize =
      projectile.type === "knife"
        ? LAYOUT.HERO_KNIFE_RANGE * 2
        : projectile.type === "flamethrower"
          ? 24
          : projectile.type === "grenade"
            ? 14
            : 6;
    const projectileHitbox = {
      x: projectile.x - hitSize / 2,
      y: projectile.y - hitSize / 2,
      width: hitSize,
      height: hitSize,
    };
    const hitEnemy = enemies.find((enemy) =>
      intersects(projectileHitbox, enemy),
    );
    const hitEnemiesForProjectile =
      projectile.type === "knife"
        ? enemies.filter((enemy) => intersects(projectileHitbox, enemy))
        : hitEnemy
          ? [hitEnemy]
          : [];

    if (hitEnemiesForProjectile.length > 0) {
      for (const enemy of hitEnemiesForProjectile) {
        hitEnemies.add(enemy);
        impactEffects = addImpactEffect(enemy, impactEffects);
      }
    } else {
      remainingProjectiles.push(projectile);
    }
  }
  enemies = enemies.filter((enemy) => !hitEnemies.has(enemy));
  projectiles = remainingProjectiles;
  impactEffects = clearExpiredEffects(updateEffectAges(impactEffects, dt));

  // Scroll PDF — synced to player X position
  const nextReaderTop = uiContainer.classList.contains("minimized")
    ? uiContainer.getBoundingClientRect().bottom + 12
    : 0;
  if (nextReaderTop !== readerTop) {
    readerTop = nextReaderTop;
    bgNeedsRedraw = true;
  }
  if (offscreenCanvas.height > 0) {
    const dpr = renderedDpr;
    const screenPdfH = offscreenCanvas.height / dpr;
    const safeReadingHeight = Math.max(
      1,
      gameInfo.bookHeight - LAYOUT.SAFE_READING_MARGIN - readerTop,
    );
    const maxScroll = Math.max(0, screenPdfH - safeReadingHeight);

    const percent = clamp(
      player.x / Math.max(1, gameInfo.width - player.width),
      0,
      1,
    );
    scrollY = percent * maxScroll;
  }
  heroSpeech.update(performance.now());
}

/** Move placeholder enemies according to their type. */
function updateEnemies(dt) {
  const playerCenterX = player.x + player.width / 2;
  const playerCenterY = player.y + player.height / 2;

  for (const enemy of enemies) {
    if (enemy.type === "crawler") {
      enemy.x += enemy.vx * dt;
      if (enemy.x < enemy.minX) {
        enemy.x = enemy.minX;
        enemy.vx = Math.abs(enemy.vx);
      }
      if (enemy.x + enemy.width > enemy.maxX) {
        enemy.x = enemy.maxX - enemy.width;
        enemy.vx = -Math.abs(enemy.vx);
      }
      continue;
    }

    if (enemy.type === "flyer") {
      const enemyCenterX = enemy.x + enemy.width / 2;
      const enemyCenterY = enemy.y + enemy.height / 2;
      const dx = playerCenterX - enemyCenterX;
      const dy = playerCenterY - enemyCenterY;
      const distance = Math.max(1, Math.hypot(dx, dy));
      enemy.x += (dx / distance) * LAYOUT.FLYER_SPEED * dt;
      enemy.y += (dy / distance) * LAYOUT.FLYER_SPEED * dt;
    }
  }
}

/** Draw the PDF background layer (only when scroll changed or flagged). */
function drawBackground() {
  if (Math.abs(scrollY - lastScrollY) <= 0.5 && !bgNeedsRedraw) return;

  const dpr = renderedDpr;
  bgCtx.fillStyle = "#fff";
  bgCtx.clearRect(0, 0, gameInfo.width, gameInfo.bookHeight);
  bgCtx.fillRect(0, 0, gameInfo.width, gameInfo.bookHeight);

  if (pdfDoc && offscreenCanvas.width > 0) {
    const sourceX = Math.max(
      0,
      Math.round((offscreenCanvas.width - gameInfo.width * dpr) / 2),
    );
    const sourceY = Math.round(scrollY * dpr);
    const sourceW = Math.min(
      offscreenCanvas.width - sourceX,
      Math.round(gameInfo.width * dpr),
    );
    const sourceH = Math.min(
      offscreenCanvas.height - sourceY,
      Math.round(Math.max(1, gameInfo.bookHeight - readerTop) * dpr),
    );
    const destW = sourceW / dpr;
    const destX = (gameInfo.width - destW) / 2;

    if (sourceH > 0) {
      bgCtx.drawImage(
        offscreenCanvas,
        sourceX,
        sourceY,
        sourceW,
        sourceH,
        destX,
        readerTop,
        destW,
        sourceH / dpr,
      );
    }
  }

  lastScrollY = scrollY;
  bgNeedsRedraw = false;
}

/** Main draw orchestrator. */
function draw() {
  drawBackground();

  gameCtx.clearRect(0, 0, gameInfo.width, gameInfo.gameHeight);
  drawScene({
    terrain,
    terrainTheme,
    enemies,
    projectiles,
    impactEffects,
    animationFrame,
  });
}

// ---------------------------------------------------------------------------
// Game loop — delta-time normalised to ~60 fps
// ---------------------------------------------------------------------------

let lastTime = 0;

/**
 * Main game loop with frame-rate independent timing.
 * @param {DOMHighResTimeStamp} timestamp
 */
function gameLoop(timestamp) {
  // Clamp dt to avoid spiral-of-death on tab-switch / heavy lag
  const dt = clamp((timestamp - lastTime) / 16.667, 0.1, 3);
  lastTime = timestamp;

  update(dt);
  draw();
  gameLoopId = requestAnimationFrame(gameLoop);
}

const drawScene = createRenderer({
  gameCtx,
  sprites,
  gameInfo,
  player,
  heroSpeech,
});

// Initial draw
draw();
