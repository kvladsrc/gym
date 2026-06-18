import { loadSprites } from "./assets.js";
import { maybeFireAtTarget, updateProjectiles } from "./combat.js";
import { LAYOUT, PHYSICS } from "./config.js";
import {
  addImpactEffect,
  clearExpiredEffects,
  updateEffectAges,
} from "./effects.js";
import { isMoving, isMovingLeft, isMovingRight, keys } from "./input.js";
import { clamp, intersects } from "./math.js";
import { HeroSpeech } from "./speech.js";
import { createPageWorld, terrainGroundAtX } from "./world.js";

const pdfjsLib = window.pdfjsLib;

// PDF.js settings
pdfjsLib.GlobalWorkerOptions.workerSrc =
  "https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js";

// ---------------------------------------------------------------------------
// DOM Elements
// ---------------------------------------------------------------------------

const uploadInput = document.getElementById("pdf-upload");
const uiContainer = document.getElementById("ui-container");
const controls = document.getElementById("controls");
const loading = document.getElementById("loading");
const pageNumInput = document.getElementById("page-num");
const pageCountSpan = document.getElementById("page-count");
const zoomOutButton = document.getElementById("zoom-out");
const zoomInButton = document.getElementById("zoom-in");
const zoomValue = document.getElementById("zoom-value");
const openaiKeyInput = document.getElementById("openai-key");

// Canvas Elements
const bgCanvas = document.getElementById("bg-canvas");
const gameCanvas = document.getElementById("game-canvas");
const bgCtx = bgCanvas.getContext("2d");
const gameCtx = gameCanvas.getContext("2d");

const offscreenCanvas = document.getElementById("offscreen-pdf");
const offscreenCtx = offscreenCanvas.getContext("2d");

// ---------------------------------------------------------------------------
// State
// ---------------------------------------------------------------------------

let pdfDoc = null;
let pageNum = 1;
let pageRendering = false;
let pageNumPending = null;
let gameLoopId = null;
let scrollY = 0;
let lastScrollY = -1;
let bgNeedsRedraw = true;
let resizeTimeout = null;
let pdfZoom = 1;
let terrain = [];
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
  jetpackActive: false,
  turboJetpackActive: false,
};

// ---------------------------------------------------------------------------
// Resize
// ---------------------------------------------------------------------------

function resize() {
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

  bgNeedsRedraw = true;

  if (pdfDoc) {
    clearTimeout(resizeTimeout);
    resizeTimeout = setTimeout(() => {
      queueRenderPage(pageNum);
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
  if (num > 0 && num <= pdfDoc.numPages) {
    queueRenderPage(num);
  } else {
    pageNumInput.value = pageNum;
  }
});

zoomOutButton.addEventListener("click", () =>
  setPdfZoom(pdfZoom - LAYOUT.PDF_ZOOM_STEP),
);
zoomInButton.addEventListener("click", () =>
  setPdfZoom(pdfZoom + LAYOUT.PDF_ZOOM_STEP),
);

window.addEventListener("keydown", (e) => {
  if (e.code in keys) {
    keys[e.code] = true;
  }
  if (e.code in keys) {
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

window.addEventListener(
  "wheel",
  (e) => {
    if (!pdfDoc) return;
    e.preventDefault();
    wheelTurboVelocity = clamp(
      wheelTurboVelocity + e.deltaY * LAYOUT.WHEEL_TURBO_SCALE,
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
  if (!file || file.type !== "application/pdf") return;

  loading.classList.remove("hidden");
  document.getElementById("upload-wrapper").classList.add("hidden");

  try {
    const arrayBuffer = await file.arrayBuffer();
    pdfDoc = await pdfjsLib.getDocument({ data: arrayBuffer }).promise;

    controls.classList.remove("hidden");
    loading.classList.add("hidden");
    uiContainer.classList.add("minimized");

    pageCountSpan.textContent = pdfDoc.numPages;
    pageNumInput.max = pdfDoc.numPages;

    pageNum = 1;
    player.x = 0;
    await renderPage(pageNum);

    if (!gameLoopId) {
      gameLoopId = requestAnimationFrame(gameLoop);
    }
  } catch (err) {
    console.error("Error rendering PDF:", err);
    loading.textContent = "ERROR LOADING PDF.";
    document.getElementById("upload-wrapper").classList.remove("hidden");
  }
}

/**
 * Render a PDF page into the offscreen canvas.
 * @param {number} num — 1-indexed page number
 */
async function renderPage(num) {
  pageRendering = true;
  pageNum = num;
  pageNumInput.value = num;

  const page = await pdfDoc.getPage(num);

  const viewportForScale = page.getViewport({ scale: 1 });
  const dpr = window.devicePixelRatio || 1;
  const scale = (gameInfo.width / viewportForScale.width) * dpr * pdfZoom;
  const scaledViewport = page.getViewport({ scale });

  offscreenCanvas.width = scaledViewport.width;
  offscreenCanvas.height = scaledViewport.height;

  // Fill white for transparent pages
  offscreenCtx.fillStyle = "#ffffff";
  offscreenCtx.fillRect(0, 0, offscreenCanvas.width, offscreenCanvas.height);

  await page.render({
    canvasContext: offscreenCtx,
    viewport: scaledViewport,
  }).promise;

  const textContent = await page.getTextContent();
  const pageText = textContent.items
    .map((item) => ("str" in item ? item.str : ""))
    .join(" ");

  pageRendering = false;
  bgNeedsRedraw = true;
  generateWorld();
  heroSpeech.maybeRequest(pageNum, pageText);

  if (pageNumPending !== null) {
    const pending = pageNumPending;
    pageNumPending = null;
    renderPage(pending);
  }
}

/** @param {number} nextZoom */
function setPdfZoom(nextZoom) {
  pdfZoom = clamp(nextZoom, LAYOUT.PDF_ZOOM_MIN, LAYOUT.PDF_ZOOM_MAX);
  zoomValue.textContent = `${Math.round(pdfZoom * 100)}%`;
  bgNeedsRedraw = true;
  if (pdfDoc) {
    queueRenderPage(pageNum);
  }
}

/**
 * Generate deterministic world objects for the current page.
 * Seed is based on pageNum so terrain and actors stay consistent.
 */
function generateWorld() {
  const world = createPageWorld(pageNum, gameInfo);
  terrain = world.terrain;
  enemies = world.enemies;
  impactEffects = [];
  projectiles = [];
}

/**
 * Queue a page render — defers if a render is already in progress.
 * @param {number} num — 1-indexed page number
 */
function queueRenderPage(num) {
  if (pageRendering) {
    pageNumPending = num;
  } else {
    renderPage(num);
  }
}

function onPrevPage() {
  if (pageNum <= 1) return;
  player.x = gameInfo.width - player.width;
  queueRenderPage(pageNum - 1);
}

function onNextPage() {
  if (pageNum >= pdfDoc.numPages) return;
  player.x = 0;
  queueRenderPage(pageNum + 1);
}

// ---------------------------------------------------------------------------
// Game engine — update (delta-time normalised to ~60 fps)
// ---------------------------------------------------------------------------

/**
 * Update game state. All physics values are normalised so dt ≈ 1.0 at 60 fps.
 * @param {number} dt — frame delta normalised to 60 fps (1.0 = 16.667 ms)
 */
function update(dt) {
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
  if (offscreenCanvas.height > 0) {
    const dpr = window.devicePixelRatio || 1;
    const screenPdfH = offscreenCanvas.height / dpr;
    const safeReadingHeight = gameInfo.bookHeight - LAYOUT.SAFE_READING_MARGIN;
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

/** Draw placeholder enemies. */
function drawEnemies() {
  for (const enemy of enemies) {
    gameCtx.save();
    if (enemy.type === "flyer") {
      gameCtx.fillStyle = "#111";
      gameCtx.beginPath();
      gameCtx.arc(
        enemy.x + enemy.width / 2,
        enemy.y + enemy.height / 2,
        enemy.width / 2,
        0,
        Math.PI * 2,
      );
      gameCtx.fill();
      gameCtx.fillStyle = "#fff";
      gameCtx.fillRect(enemy.x + 9, enemy.y + 12, 5, 5);
      gameCtx.fillRect(enemy.x + enemy.width - 14, enemy.y + 12, 5, 5);
    } else {
      gameCtx.fillStyle = "#111";
      gameCtx.fillRect(enemy.x, enemy.y, enemy.width, enemy.height);
      gameCtx.fillStyle = "#fff";
      gameCtx.fillRect(enemy.x + 8, enemy.y + 9, 5, 5);
      gameCtx.fillRect(enemy.x + enemy.width - 13, enemy.y + 9, 5, 5);
    }
    gameCtx.restore();
  }
}

/** Draw placeholder bullets. */
function drawProjectiles() {
  for (const projectile of projectiles) {
    gameCtx.save();
    gameCtx.fillStyle = "#000";
    if (projectile.type === "knife") {
      gameCtx.globalAlpha = 0.35;
      gameCtx.strokeStyle = "#000";
      gameCtx.lineWidth = 3;
      gameCtx.beginPath();
      gameCtx.arc(
        projectile.x,
        projectile.y,
        LAYOUT.HERO_KNIFE_RANGE,
        0,
        Math.PI * 2,
      );
      gameCtx.stroke();
    } else if (projectile.type === "flamethrower") {
      gameCtx.globalAlpha = 0.45;
      gameCtx.beginPath();
      gameCtx.arc(projectile.x, projectile.y, 18, 0, Math.PI * 2);
      gameCtx.fill();
    } else if (projectile.type === "grenade") {
      gameCtx.beginPath();
      gameCtx.arc(projectile.x, projectile.y, 7, 0, Math.PI * 2);
      gameCtx.fill();
    } else {
      gameCtx.beginPath();
      gameCtx.arc(projectile.x, projectile.y, 4, 0, Math.PI * 2);
      gameCtx.fill();
    }
    gameCtx.restore();
  }
}

// ---------------------------------------------------------------------------
// Game engine — draw (decomposed into sub-functions)
// ---------------------------------------------------------------------------

/** Draw the PDF background layer (only when scroll changed or flagged). */
function drawBackground() {
  if (Math.abs(scrollY - lastScrollY) <= 0.5 && !bgNeedsRedraw) return;

  const dpr = window.devicePixelRatio || 1;
  bgCtx.fillStyle = "#fff";
  bgCtx.clearRect(0, 0, gameInfo.width, gameInfo.bookHeight);
  bgCtx.fillRect(0, 0, gameInfo.width, gameInfo.bookHeight);

  if (pdfDoc && !pageRendering) {
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
      Math.round(gameInfo.bookHeight * dpr),
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
        0,
        destW,
        sourceH / dpr,
      );
    }
  }

  lastScrollY = scrollY;
  bgNeedsRedraw = false;
}

/** Draw the tiled terrain floor. */
function drawTerrain() {
  const terrainSegments =
    terrain.length > 0 ? terrain : [{ x: 0, width: gameInfo.width, level: 0 }];

  for (const segment of terrainSegments) {
    if (segment.level === 2) {
      continue;
    }

    const surfaceY =
      gameInfo.groundY - segment.level * LAYOUT.TERRAIN_LEVEL_HEIGHT;
    const blockSize = LAYOUT.TERRAIN_SEGMENT_WIDTH;
    const drawY = surfaceY;

    gameCtx.save();
    gameCtx.fillStyle = segment.level === 0 ? "#f7f7f7" : "#e8e8e8";
    gameCtx.strokeStyle = "#000";
    gameCtx.lineWidth = 2;
    gameCtx.fillRect(segment.x, drawY, blockSize, blockSize);
    gameCtx.strokeRect(segment.x, drawY, blockSize, blockSize);

    gameCtx.fillStyle = "rgba(0, 0, 0, 0.08)";
    gameCtx.fillRect(segment.x + 6, drawY + 6, blockSize - 12, 8);
    gameCtx.restore();
  }
}

/** Draw a simple jetpack flame while the hero crosses gaps or level changes. */
function drawJetpack(px, py, pw, ph) {
  if (!player.jetpackActive) return;

  gameCtx.save();
  gameCtx.fillStyle = "#000";
  const flameX = px + pw / 2 - 5;
  const flameY = py + ph - 2;
  gameCtx.beginPath();
  gameCtx.moveTo(flameX, flameY);
  gameCtx.lineTo(flameX + 10, flameY);
  gameCtx.lineTo(flameX + 5, flameY + 14 + Math.sin(animationFrame * 0.6) * 4);
  gameCtx.closePath();
  gameCtx.fill();
  if (player.turboJetpackActive) {
    gameCtx.fillRect(
      px + (player.facingRight ? -18 : pw + 6),
      py + ph * 0.35,
      24,
      8,
    );
  }
  gameCtx.restore();
}

/** Draw short-lived impact flashes for hits. */
function drawImpactEffects() {
  for (const effect of impactEffects) {
    const progress = clamp(effect.age / effect.ttl, 0, 1);
    const radius = 8 + progress * 24;

    gameCtx.save();
    gameCtx.globalAlpha = 1 - progress;
    gameCtx.strokeStyle = "#000";
    gameCtx.lineWidth = 3;
    gameCtx.beginPath();
    gameCtx.moveTo(effect.x, effect.y - radius);
    gameCtx.lineTo(effect.x + radius, effect.y);
    gameCtx.lineTo(effect.x, effect.y + radius);
    gameCtx.lineTo(effect.x - radius, effect.y);
    gameCtx.closePath();
    gameCtx.stroke();
    gameCtx.restore();
  }
}

/** Draw the hero's short automatic melee shockwave. */
function drawAttackArc(px, py, pw, ph) {
  if (player.attackTimer <= 0) return;

  const progress = 1 - player.attackTimer / LAYOUT.ATTACK_TTL_FRAMES;
  const radius = LAYOUT.HERO_KNIFE_RANGE * (0.75 + progress * 0.25);

  gameCtx.save();
  gameCtx.globalAlpha = 1 - progress * 0.35;
  gameCtx.strokeStyle = "#000";
  gameCtx.lineWidth = 4;
  gameCtx.beginPath();
  gameCtx.arc(px + pw / 2, py + ph / 2, radius, 0, Math.PI * 2);
  gameCtx.stroke();
  gameCtx.restore();
}

/** Draw the current LLM-generated hero line, if any. */
function drawSpeechBubble() {
  const text = heroSpeech.currentText();
  if (!text) return;

  const maxWidth = 280;
  const lineHeight = 18;
  const words = text.split(/\s+/);
  const lines = [];
  let currentLine = "";

  gameCtx.save();
  gameCtx.font = "bold 14px 'Courier New', monospace";
  for (const word of words) {
    const candidate = currentLine ? `${currentLine} ${word}` : word;
    if (gameCtx.measureText(candidate).width > maxWidth && currentLine) {
      lines.push(currentLine);
      currentLine = word;
    } else {
      currentLine = candidate;
    }
  }
  if (currentLine) lines.push(currentLine);

  const bubbleWidth =
    Math.min(
      maxWidth,
      Math.max(...lines.map((line) => gameCtx.measureText(line).width)),
    ) + 24;
  const bubbleHeight = lines.length * lineHeight + 20;
  const x = clamp(
    player.x + player.width / 2 - bubbleWidth / 2,
    8,
    gameInfo.width - bubbleWidth - 8,
  );
  const y = clamp(player.y - bubbleHeight - 18, 8, gameInfo.gameHeight - 80);

  gameCtx.fillStyle = "#fff";
  gameCtx.strokeStyle = "#000";
  gameCtx.lineWidth = 3;
  gameCtx.fillRect(x, y, bubbleWidth, bubbleHeight);
  gameCtx.strokeRect(x, y, bubbleWidth, bubbleHeight);

  gameCtx.beginPath();
  gameCtx.moveTo(x + bubbleWidth / 2 - 8, y + bubbleHeight);
  gameCtx.lineTo(x + bubbleWidth / 2 + 8, y + bubbleHeight);
  gameCtx.lineTo(player.x + player.width / 2, y + bubbleHeight + 12);
  gameCtx.closePath();
  gameCtx.fill();
  gameCtx.stroke();

  gameCtx.fillStyle = "#000";
  lines.forEach((line, index) => {
    gameCtx.fillText(line, x + 12, y + 18 + index * lineHeight);
  });
  gameCtx.restore();
}

/** Draw the player sprite (with flip and squash-stretch). */
function drawPlayer() {
  let pw = player.width;
  let ph = player.height;
  const bob = Math.sin(animationFrame * 0.2) * 2;

  if (!player.isGrounded) {
    ph += 2;
    pw -= 2;
  } else if (Math.abs(player.vx) > 3) {
    pw += 2;
    ph -= 2;
  }

  const px = Math.round(player.x + (player.width - pw) / 2);
  const py = Math.round(player.y + (player.height - ph) + 6 + bob);

  drawAttackArc(px, py, pw, ph);
  drawJetpack(px, py, pw, ph);

  gameCtx.save();
  if (player.facingRight) {
    gameCtx.translate(px + pw / 2, py);
    gameCtx.scale(-1, 1);
    gameCtx.translate(-(px + pw / 2), -py);
  }

  if (sprites.player.complete && sprites.player.naturalHeight !== 0) {
    gameCtx.drawImage(sprites.player, px, py, pw, ph);
  } else {
    gameCtx.fillStyle = "#000";
    gameCtx.fillRect(px, py, pw, ph);
  }

  gameCtx.restore();
}

/** Main draw orchestrator. */
function draw() {
  drawBackground();

  gameCtx.clearRect(0, 0, gameInfo.width, gameInfo.gameHeight);
  drawTerrain();
  drawEnemies();
  drawProjectiles();
  drawImpactEffects();
  drawPlayer();
  drawSpeechBubble();
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

// Initial draw
draw();
