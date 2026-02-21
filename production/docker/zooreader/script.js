// PDF.js settings
pdfjsLib.GlobalWorkerOptions.workerSrc =
  "https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js";

// DOM Elements
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
const offscreenCtx = offscreenCanvas.getContext("2d", {
  willReadFrequently: true,
});

// State
let pdfDoc = null;
let pageNum = 1;
let pageRendering = false;
let pageNumPending = null;
let gameLoopId = null;
let scrollY = 0;
let obstacles = [];

// Load external sprites
const playerImg = new Image();
playerImg.src = "player.png";

const barrierImg = new Image();
barrierImg.src = "barrier.png";

// Terrain tiles (order determines which page maps to which biome)
const terrainTiles = ["grass.png", "desert.png", "ice.png", "lava.png"].map(
  (src) => {
    const img = new Image();
    img.src = src;
    return img;
  },
);

function getCurrentTile() {
  return terrainTiles[(pageNum - 1) % terrainTiles.length];
}

// Game State
const gameInfo = {
  width: window.innerWidth,
  bookHeight: window.innerHeight,
  gameHeight: window.innerHeight,
  groundY: window.innerHeight - 30,
};

const player = {
  x: 0,
  y: gameInfo.groundY - 60,
  width: 60,
  height: 60,
  vx: 0,
  vy: 0,
  speed: 8,
  jumpStrength: 11,
  gravity: 0.8,
  isGrounded: true,
  facingRight: true,
};

const keys = {
  ArrowLeft: false,
  ArrowRight: false,
  Space: false,
};

// Resize handler
function resize() {
  gameInfo.width = window.innerWidth;
  gameInfo.bookHeight = window.innerHeight;
  gameInfo.gameHeight = window.innerHeight;
  gameInfo.groundY = window.innerHeight - 30;

  // Scale for HDPI displays
  const dpr = window.devicePixelRatio || 1;

  bgCanvas.width = gameInfo.width * dpr;
  bgCanvas.height = gameInfo.bookHeight * dpr;

  gameCanvas.width = gameInfo.width * dpr;
  gameCanvas.height = gameInfo.gameHeight * dpr;

  bgCtx.setTransform(dpr, 0, 0, dpr, 0, 0);
  gameCtx.setTransform(dpr, 0, 0, dpr, 0, 0);

  if (pdfDoc && !pageRendering) {
    renderPage(pageNum);
  }
}
window.addEventListener("resize", resize);
resize();

// Event Listeners for UI
uploadInput.addEventListener("change", handleUpload);

pageNumInput.addEventListener("change", (e) => {
  let num = parseInt(e.target.value);
  if (num > 0 && num <= pdfDoc.numPages) {
    queueRenderPage(num);
  } else {
    pageNumInput.value = pageNum;
  }
});

// Key Listeners
window.addEventListener("keydown", (e) => {
  if (keys.hasOwnProperty(e.code) || e.code === "Space") {
    if (e.code === "Space") e.preventDefault(); // prevent falling scroll
    if (e.code === "Space") keys.Space = true;
    if (e.code === "ArrowLeft") keys.ArrowLeft = true;
    if (e.code === "ArrowRight") keys.ArrowRight = true;
  }
});

window.addEventListener("keyup", (e) => {
  if (keys.hasOwnProperty(e.code) || e.code === "Space") {
    if (e.code === "Space") keys.Space = false;
    if (e.code === "ArrowLeft") keys.ArrowLeft = false;
    if (e.code === "ArrowRight") keys.ArrowRight = false;
  }
});

// PDF Handling
async function handleUpload(e) {
  const file = e.target.files[0];
  if (file && file.type === "application/pdf") {
    loading.classList.remove("hidden");
    document.getElementById("upload-wrapper").classList.add("hidden");
    const arrayBuffer = await file.arrayBuffer();

    try {
      pdfDoc = await pdfjsLib.getDocument({ data: arrayBuffer }).promise;

      controls.classList.remove("hidden");
      loading.classList.add("hidden");
      uiContainer.classList.add("minimized");

      pageCountSpan.textContent = pdfDoc.numPages;
      pageNumInput.max = pdfDoc.numPages;

      pageNum = 1;
      player.x = 0; // start at top left of first page
      renderPage(pageNum);

      if (!gameLoopId) {
        gameLoopId = requestAnimationFrame(gameLoop);
      }
    } catch (err) {
      console.error("Error rendering PDF:", err);
      loading.textContent = "ERROR LOADING PDF.";
      document.getElementById("upload-wrapper").classList.remove("hidden");
    }
  }
}

function renderPage(num) {
  pageRendering = true;
  pageNum = num;

  pdfDoc.getPage(num).then((page) => {
    // Calculate scale to fit width exactly
    const viewportForScale = page.getViewport({ scale: 1 });
    // The dpr affects offscreen canvas internal resolution so it looks crisp
    const dpr = window.devicePixelRatio || 1;
    const scale = (gameInfo.width / viewportForScale.width) * dpr;

    const scaledViewport = page.getViewport({ scale });

    offscreenCanvas.width = scaledViewport.width;
    offscreenCanvas.height = scaledViewport.height;

    const renderContext = {
      canvasContext: offscreenCtx,
      viewport: scaledViewport,
    };

    // Fill white for transparent pages
    offscreenCtx.fillStyle = "#ffffff";
    offscreenCtx.fillRect(0, 0, offscreenCanvas.width, offscreenCanvas.height);

    const renderTask = page.render(renderContext);

    renderTask.promise.then(() => {
      pageRendering = false;
      generateObstacles();
      if (pageNumPending !== null) {
        renderPage(pageNumPending);
        pageNumPending = null;
      }
    });
  });

  pageNumInput.value = num;
}

function generateObstacles() {
  obstacles = [];
  // deterministic based on pageNum so it doesn't change back and forth
  const seed = pageNum * 137;
  const numObstacles = 1 + (seed % 3); // 1 to 3 spikes per page

  let lastX = gameInfo.width * 0.2;
  for (let i = 0; i < numObstacles; i++) {
    let obX = lastX + 150 + ((seed * 73 + i * 777) % 150);
    if (obX < gameInfo.width - 50) {
      obstacles.push({
        x: obX,
        y: gameInfo.groundY - 22,
        width: 28,
        height: 28,
      });
      lastX = obX;
    }
  }
}

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

// Game Engine
function update() {
  // Horizontal movement
  if (keys.ArrowRight) {
    player.vx += 1.5;
    player.facingRight = true;
  } else if (keys.ArrowLeft) {
    player.vx -= 1.5;
    player.facingRight = false;
  } else {
    // Friction
    player.vx *= 0.7;
  }

  // Cap speed
  if (player.vx > player.speed) player.vx = player.speed;
  if (player.vx < -player.speed) player.vx = -player.speed;

  // Move
  player.x += player.vx;

  // Handle Page Bounds
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

  // Update PDF Scroll Y corresponding to X %
  if (offscreenCanvas.height > 0) {
    const dpr = window.devicePixelRatio || 1;
    const screenPdfH = offscreenCanvas.height / dpr;

    // Add a safe reading margin (150px) to prevent bottom text from being obscured by gradient fade
    let safeReadingHeight = gameInfo.bookHeight - 150;
    let maxScroll = Math.max(0, screenPdfH - safeReadingHeight);

    let percent = player.x / Math.max(1, gameInfo.width - player.width);
    percent = Math.max(0, Math.min(1, percent));

    // Perfectly synched scroll that pushes the bottom of the page above the fade zone
    scrollY = percent * maxScroll;
  }

  // Jump
  if (keys.Space && player.isGrounded) {
    player.vy = -player.jumpStrength;
    player.isGrounded = false;
  }

  // Gravity
  player.vy += player.gravity;
  player.y += player.vy;

  // Ground collision
  if (player.y + player.height >= gameInfo.groundY) {
    player.y = gameInfo.groundY - player.height;
    player.vy = 0;
    player.isGrounded = true;
  }

  // Obstacle collision
  for (let ob of obstacles) {
    // hit box is slightly smaller than drawn visual so it's less frustrating
    let hitMargin = 4;
    if (
      player.x + hitMargin < ob.x + ob.width &&
      player.x + player.width - hitMargin > ob.x &&
      player.y + hitMargin < ob.y + ob.height &&
      player.y + player.height > ob.y
    ) {
      // Knockback logic
      player.vx = player.facingRight ? -8 : 8;
      player.vy = -6;
      player.isGrounded = false;
    }
  }
}

function draw() {
  // Clear backgrounds
  bgCtx.clearRect(0, 0, gameInfo.width, gameInfo.bookHeight);
  gameCtx.clearRect(0, 0, gameInfo.width, gameInfo.gameHeight);

  // Draw PDF
  if (pdfDoc && !pageRendering) {
    const dpr = window.devicePixelRatio || 1;

    // We need to slice the offscreen canvas based on scrollY
    const sourceX = 0;
    const sourceY = scrollY * dpr;
    const sourceW = gameInfo.width * dpr;
    // The slice height is the height of the viewing area
    const sourceH = Math.min(
      offscreenCanvas.height - sourceY,
      gameInfo.bookHeight * dpr,
    );

    if (sourceH > 0) {
      bgCtx.drawImage(
        offscreenCanvas,
        sourceX,
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
    // Just fill white
    bgCtx.fillStyle = "#fff";
    bgCtx.fillRect(0, 0, gameInfo.width, gameInfo.bookHeight);
  }

  // Draw Game Area — tiled terrain floor
  const tile = getCurrentTile();
  if (tile.complete && tile.naturalHeight !== 0) {
    // Render at a fixed display height so tiles are always the right size
    // regardless of source image resolution.
    const TILE_H = 64;
    const TILE_W =
      tile.naturalWidth > 0
        ? Math.round((TILE_H * tile.naturalWidth) / tile.naturalHeight)
        : 64;
    // Start 25% of tile height ABOVE groundY so the transparent top quarter
    // blends into the game area and the opaque part fills the visible floor strip.
    const startY = gameInfo.groundY - TILE_H * 0.25;
    const rows = Math.ceil((gameInfo.gameHeight - startY) / TILE_H) + 1;
    for (let row = 0; row < rows; row++) {
      const drawY = startY + row * TILE_H;
      for (let x = 0; x < gameInfo.width; x += TILE_W) {
        gameCtx.drawImage(tile, x, drawY, TILE_W, TILE_H);
      }
    }
  } else {
    // Fallback dots while tiles load
    gameCtx.fillStyle = "#000";
    for (let i = 0; i < gameInfo.width; i += 40) {
      gameCtx.fillRect(i, gameInfo.groundY + 6, 4, 4);
    }
  }

  // Draw obstacles
  for (let ob of obstacles) {
    if (barrierImg.complete && barrierImg.naturalHeight !== 0) {
      gameCtx.drawImage(barrierImg, ob.x, ob.y, ob.width, ob.height);
    } else {
      // Fallback triangle while image loads
      gameCtx.fillStyle = "#000";
      gameCtx.beginPath();
      gameCtx.moveTo(ob.x, ob.y + ob.height);
      gameCtx.lineTo(ob.x + ob.width / 2, ob.y);
      gameCtx.lineTo(ob.x + ob.width, ob.y + ob.height);
      gameCtx.fill();
    }
  }

  // Draw Player (External Sprite)
  let pw = player.width;
  let ph = player.height;

  if (!player.isGrounded) {
    ph += 2;
    pw -= 2;
  } else if (Math.abs(player.vx) > 3) {
    pw += 2;
    ph -= 2;
  }

  const px = player.x + (player.width - pw) / 2;
  const py = player.y + (player.height - ph) + 6; // slight nudge down to sit on tiles

  gameCtx.save();
  // Image naturally faces left, so flip it when moving right
  if (player.facingRight) {
    // Flip image horizontally
    gameCtx.translate(px + pw / 2, py);
    gameCtx.scale(-1, 1);
    gameCtx.translate(-(px + pw / 2), -py);
  }

  // Check if image is loaded, otherwise fallback to black square
  if (playerImg.complete && playerImg.naturalHeight !== 0) {
    gameCtx.drawImage(playerImg, px, py, pw, ph);
  } else {
    gameCtx.fillStyle = "#000";
    gameCtx.fillRect(px, py, pw, ph);
  }

  gameCtx.restore();
}

function gameLoop() {
  update();
  draw();
  gameLoopId = requestAnimationFrame(gameLoop);
}

// Initial draw
draw();
