// PDF.js settings
pdfjsLib.GlobalWorkerOptions.workerSrc =
  "https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js";

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

/** Physics tuning knobs, normalised to 60 fps via delta-time. */
const PHYSICS = {
  ACCELERATION: 1.5,
  FRICTION: 0.7,
  KNOCKBACK_VX: 8,
  KNOCKBACK_VY: -6,
  HIT_MARGIN: 4,
};

/** Visual / layout constants. */
const LAYOUT = {
  TILE_SIZE: 64,
  SAFE_READING_MARGIN: 150,
  PLAYER_SIZE: 60,
  GROUND_OFFSET: 30,
  OBSTACLE_SIZE: 28,
  RESIZE_DEBOUNCE_MS: 200,
};

// ---------------------------------------------------------------------------
// Utilities
// ---------------------------------------------------------------------------

/**
 * Clamp a value between min and max (inclusive).
 * @param {number} v
 * @param {number} min
 * @param {number} max
 * @returns {number}
 */
const clamp = (v, min, max) => Math.max(min, Math.min(max, v));

// ---------------------------------------------------------------------------
// DOM Elements
// ---------------------------------------------------------------------------

const uploadInput = document.getElementById("pdf-upload");
const uiContainer = document.getElementById("ui-container");
const controls = document.getElementById("controls");
const loading = document.getElementById("loading");
const pageNumInput = document.getElementById("page-num");
const pageCountSpan = document.getElementById("page-count");

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
let obstacles = [];

// ---------------------------------------------------------------------------
// Sprites
// ---------------------------------------------------------------------------

const playerImg = new Image();
playerImg.src = "player.png";

const barrierImg = new Image();
barrierImg.src = "barrier.png";

/** Terrain tiles — order determines which page maps to which biome. */
const terrainTiles = ["grass.png", "desert.png", "ice.png", "lava.png"].map(
  (src) => {
    const img = new Image();
    img.src = src;
    return img;
  },
);

/** @returns {HTMLImageElement} tile for the current page */
function getCurrentTile() {
  return terrainTiles[(pageNum - 1) % terrainTiles.length];
}

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
  jumpStrength: 14,
  gravity: 0.8,
  isGrounded: true,
  facingRight: true,
};

const keys = {
  ArrowLeft: false,
  ArrowRight: false,
  Space: false,
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

window.addEventListener("keydown", (e) => {
  if (e.code in keys) {
    keys[e.code] = true;
  }
  if (e.code === "Space") {
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
  const scale = (gameInfo.width / viewportForScale.width) * dpr;
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

  pageRendering = false;
  bgNeedsRedraw = true;
  generateObstacles();

  if (pageNumPending !== null) {
    const pending = pageNumPending;
    pageNumPending = null;
    renderPage(pending);
  }
}

/**
 * Generate deterministic obstacles for the current page.
 * Seed is based on pageNum so obstacles stay consistent when revisiting.
 */
function generateObstacles() {
  obstacles = [];
  const seed = pageNum * 137;
  const numObstacles = 1 + (seed % 3);

  let lastX = gameInfo.width * 0.2;
  for (let i = 0; i < numObstacles; i++) {
    const obX = lastX + 150 + ((seed * 73 + i * 777) % 150);
    if (obX < gameInfo.width - 50) {
      obstacles.push({
        x: obX,
        y: gameInfo.groundY - LAYOUT.OBSTACLE_SIZE + 6,
        width: LAYOUT.OBSTACLE_SIZE,
        height: LAYOUT.OBSTACLE_SIZE,
      });
      lastX = obX;
    }
  }
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
  // Horizontal movement
  if (keys.ArrowRight) {
    player.vx += PHYSICS.ACCELERATION * dt;
    player.facingRight = true;
  } else if (keys.ArrowLeft) {
    player.vx -= PHYSICS.ACCELERATION * dt;
    player.facingRight = false;
  } else {
    // Friction — applied per-frame; raise to dt power for frame-rate independence
    player.vx *= Math.pow(PHYSICS.FRICTION, dt);
  }

  // Cap speed
  player.vx = clamp(player.vx, -player.speed, player.speed);

  // Move
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

  // Jump
  if (keys.Space && player.isGrounded) {
    player.vy = -player.jumpStrength;
    player.isGrounded = false;
  }

  // Gravity
  player.vy += player.gravity * dt;
  player.y += player.vy * dt;

  // Ground collision
  if (player.y + player.height >= gameInfo.groundY) {
    player.y = gameInfo.groundY - player.height;
    player.vy = 0;
    player.isGrounded = true;
  }

  // Obstacle collision
  for (const ob of obstacles) {
    const m = PHYSICS.HIT_MARGIN;
    if (
      player.x + m < ob.x + ob.width &&
      player.x + player.width - m > ob.x &&
      player.y + m < ob.y + ob.height &&
      player.y + player.height > ob.y
    ) {
      player.vx = player.facingRight
        ? -PHYSICS.KNOCKBACK_VX
        : PHYSICS.KNOCKBACK_VX;
      player.vy = PHYSICS.KNOCKBACK_VY;
      player.isGrounded = false;
    }
  }
}

// ---------------------------------------------------------------------------
// Game engine — draw (decomposed into sub-functions)
// ---------------------------------------------------------------------------

/** Draw the PDF background layer (only when scroll changed or flagged). */
function drawBackground() {
  if (Math.abs(scrollY - lastScrollY) <= 0.5 && !bgNeedsRedraw) return;

  const dpr = window.devicePixelRatio || 1;
  bgCtx.clearRect(0, 0, gameInfo.width, gameInfo.bookHeight);

  if (pdfDoc && !pageRendering) {
    const sourceY = Math.round(scrollY * dpr);
    const sourceW = Math.round(gameInfo.width * dpr);
    const sourceH = Math.min(
      offscreenCanvas.height - sourceY,
      Math.round(gameInfo.bookHeight * dpr),
    );

    if (sourceH > 0) {
      bgCtx.drawImage(
        offscreenCanvas,
        0,
        sourceY,
        sourceW,
        sourceH,
        0,
        0,
        gameInfo.width,
        sourceH / dpr,
      );
    }
  } else {
    bgCtx.fillStyle = "#fff";
    bgCtx.fillRect(0, 0, gameInfo.width, gameInfo.bookHeight);
  }

  lastScrollY = scrollY;
  bgNeedsRedraw = false;
}

/** Draw the tiled terrain floor. */
function drawTerrain() {
  const tile = getCurrentTile();
  if (tile.complete && tile.naturalHeight !== 0) {
    const tileH = LAYOUT.TILE_SIZE;
    const tileW =
      tile.naturalWidth > 0
        ? Math.round((tileH * tile.naturalWidth) / tile.naturalHeight)
        : LAYOUT.TILE_SIZE;

    const startY = gameInfo.groundY - tileH * 0.25;
    const rows = Math.ceil((gameInfo.gameHeight - startY) / tileH) + 1;
    for (let row = 0; row < rows; row++) {
      const drawY = startY + row * tileH;
      for (let x = 0; x < gameInfo.width; x += tileW) {
        gameCtx.drawImage(tile, x, drawY, tileW, tileH);
      }
    }
  } else {
    // Fallback dots while tiles load
    gameCtx.fillStyle = "#000";
    for (let i = 0; i < gameInfo.width; i += 40) {
      gameCtx.fillRect(i, gameInfo.groundY + 6, 4, 4);
    }
  }
}

/** Draw obstacle sprites (or fallback triangles). */
function drawObstacles() {
  for (const ob of obstacles) {
    if (barrierImg.complete && barrierImg.naturalHeight !== 0) {
      gameCtx.drawImage(barrierImg, ob.x, ob.y, ob.width, ob.height);
    } else {
      gameCtx.fillStyle = "#000";
      gameCtx.beginPath();
      gameCtx.moveTo(ob.x, ob.y + ob.height);
      gameCtx.lineTo(ob.x + ob.width / 2, ob.y);
      gameCtx.lineTo(ob.x + ob.width, ob.y + ob.height);
      gameCtx.fill();
    }
  }
}

/** Draw the player sprite (with flip and squash-stretch). */
function drawPlayer() {
  let pw = player.width;
  let ph = player.height;

  if (!player.isGrounded) {
    ph += 2;
    pw -= 2;
  } else if (Math.abs(player.vx) > 3) {
    pw += 2;
    ph -= 2;
  }

  const px = Math.round(player.x + (player.width - pw) / 2);
  const py = Math.round(player.y + (player.height - ph) + 6);

  gameCtx.save();
  if (player.facingRight) {
    gameCtx.translate(px + pw / 2, py);
    gameCtx.scale(-1, 1);
    gameCtx.translate(-(px + pw / 2), -py);
  }

  if (playerImg.complete && playerImg.naturalHeight !== 0) {
    gameCtx.drawImage(playerImg, px, py, pw, ph);
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
  drawObstacles();
  drawPlayer();
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
