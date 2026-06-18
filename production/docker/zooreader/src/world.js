import { LAYOUT } from "./config.js";

const GAP_LEVEL = 2;
const ALLOWED_NEXT_LEVELS = {
  0: [0, GAP_LEVEL],
  1: [1, GAP_LEVEL],
  [GAP_LEVEL]: [0, 1],
};

/**
 * Direct 0<->1 and 2->2 transitions are intentionally forbidden: changing
 * levels must always route through a single gap so the hero can use the
 * jetpack.
 * @param {{level: number}[]} terrain
 */
function assertValidTerrainTransitions(terrain) {
  for (let i = 1; i < terrain.length; i++) {
    const prev = terrain[i - 1].level;
    const next = terrain[i].level;
    if ((prev === 0 && next === 1) || (prev === 1 && next === 0)) {
      throw new Error(`Invalid terrain transition ${prev}${next} at ${i}`);
    }
    if (prev === GAP_LEVEL && next === GAP_LEVEL) {
      throw new Error(`Invalid terrain transition 22 at ${i}`);
    }
  }
}

/**
 * @param {number} level
 * @param {{groundY: number}} gameInfo
 * @returns {number}
 */
function groundYForLevel(level, gameInfo) {
  return gameInfo.groundY - level * LAYOUT.TERRAIN_LEVEL_HEIGHT;
}

/**
 * @param {number} v
 * @param {number} min
 * @param {number} max
 * @returns {number}
 */
function clamp(v, min, max) {
  return Math.max(min, Math.min(max, v));
}

/**
 * @param {number} seed
 * @returns {() => number}
 */
function createRandom(seed) {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/**
 * @param {number} seed
 * @param {{width: number}} gameInfo
 * @returns {{x: number, width: number, level: number}[]}
 */
function createTerrain(seed, gameInfo) {
  const count = Math.ceil(gameInfo.width / LAYOUT.TERRAIN_SEGMENT_WIDTH) + 1;
  const random = createRandom(seed);
  const terrain = [
    {
      x: 0,
      width: LAYOUT.TERRAIN_SEGMENT_WIDTH,
      level: 0,
    },
  ];

  for (let i = 1; i < count; i++) {
    const prevLevel = terrain[i - 1].level;
    const nextLevels = ALLOWED_NEXT_LEVELS[prevLevel];
    const roll = Math.floor(random() * nextLevels.length);
    terrain.push({
      x: i * LAYOUT.TERRAIN_SEGMENT_WIDTH,
      width: LAYOUT.TERRAIN_SEGMENT_WIDTH,
      level: nextLevels[roll],
    });
  }

  assertValidTerrainTransitions(terrain);
  return terrain;
}

/**
 * @param {{x: number, width: number, level: number}[]} terrain
 * @param {number} index
 * @param {number} step
 * @returns {{x: number, width: number, level: number} | null}
 */
function findSolidSegment(terrain, index, step) {
  for (let i = index; i >= 0 && i < terrain.length; i += step) {
    if (terrain[i].level !== GAP_LEVEL) return terrain[i];
  }
  return null;
}

/**
 * @param {{x: number, width: number, level: number}[]} terrain
 * @param {number} x
 * @returns {{x: number, width: number, level: number}}
 */
function terrainSegmentAtX(terrain, x) {
  const index = Math.max(
    0,
    Math.min(terrain.length - 1, Math.floor(x / LAYOUT.TERRAIN_SEGMENT_WIDTH)),
  );
  return terrain[index];
}

/**
 * @param {{x: number, width: number, level: number}[]} terrain
 * @param {{x: number, width: number, level: number}} segment
 * @returns {{minX: number, maxX: number}}
 */
function solidRunBounds(terrain, segment) {
  const index = terrain.indexOf(segment);
  let start = index;
  let end = index;

  while (start > 0 && terrain[start - 1].level === segment.level) {
    start -= 1;
  }
  while (end < terrain.length - 1 && terrain[end + 1].level === segment.level) {
    end += 1;
  }

  return {
    minX: terrain[start].x,
    maxX: terrain[end].x + terrain[end].width,
  };
}

/**
 * @param {{x: number, width: number, level: number}[]} terrain
 * @param {number} x
 * @param {{groundY: number}} gameInfo
 * @returns {{groundY: number, level: number, isGap: boolean}}
 */
export function terrainGroundAtX(terrain, x, gameInfo) {
  const segment = terrainSegmentAtX(terrain, x);
  const index = terrain.indexOf(segment);
  const left = findSolidSegment(terrain, index, -1);
  const right = findSolidSegment(terrain, index, 1);

  if (segment.level === GAP_LEVEL && left && right && left !== right) {
    const progress = clamp((x - segment.x) / segment.width, 0, 1);
    const leftGroundY = groundYForLevel(left.level, gameInfo);
    const rightGroundY = groundYForLevel(right.level, gameInfo);
    const jumpArc = Math.sin(progress * Math.PI) * LAYOUT.TERRAIN_JUMP_HEIGHT;
    return {
      groundY: leftGroundY + (rightGroundY - leftGroundY) * progress - jumpArc,
      level: progress < 0.5 ? left.level : right.level,
      isGap: true,
    };
  }

  if (segment.level !== GAP_LEVEL) {
    return {
      groundY: groundYForLevel(segment.level, gameInfo),
      level: segment.level,
      isGap: false,
    };
  }

  const solid = left || right;
  const level = solid ? solid.level : 0;
  return {
    groundY: groundYForLevel(level, gameInfo),
    level,
    isGap: true,
  };
}

/**
 * Generate deterministic world objects for the current page.
 * The seed is based on page number so objects stay consistent when revisiting.
 * @param {number} pageNum
 * @param {{width: number, groundY: number}} gameInfo
 * @returns {{
 *   enemies: {
 *     type: string,
 *     x: number,
 *     y: number,
 *     width: number,
 *     height: number,
 *     hp: number,
 *     vx?: number,
 *     minX?: number,
 *     maxX?: number,
 *   }[],
 *   terrain: {x: number, width: number, level: number}[],
 * }}
 */
export function createPageWorld(pageNum, gameInfo) {
  const seed = Math.imul(pageNum, 0x9e3779b1) >>> 0;
  const numCrawlers = 2 + ((seed >> 2) % 3);
  const numFlyers = 1 + ((seed >> 6) % 2);
  const terrain = createTerrain(seed, gameInfo);
  const enemies = [];

  for (let i = 0; i < numCrawlers; i++) {
    const enemyX =
      90 + ((seed * 53 + i * 211) % Math.max(120, gameInfo.width - 180));
    const segment = terrainSegmentAtX(terrain, enemyX);
    const terrainGround = terrainGroundAtX(terrain, enemyX, gameInfo);
    if (terrainGround.isGap) continue;
    const bounds = solidRunBounds(terrain, segment);
    enemies.push({
      type: "crawler",
      x: enemyX,
      y: terrainGround.groundY - LAYOUT.ENEMY_SIZE,
      width: LAYOUT.ENEMY_SIZE,
      height: LAYOUT.ENEMY_SIZE,
      hp: 1,
      vx: i % 2 === 0 ? LAYOUT.CRAWLER_SPEED : -LAYOUT.CRAWLER_SPEED,
      minX: bounds.minX,
      maxX: bounds.maxX,
    });
  }

  for (let i = 0; i < numFlyers; i++) {
    const fromLeft = ((seed >> (8 + i)) & 1) === 0;
    enemies.push({
      type: "flyer",
      x: fromLeft ? -LAYOUT.ENEMY_SIZE : gameInfo.width + LAYOUT.ENEMY_SIZE,
      y:
        gameInfo.gameHeight * 0.52 +
        ((seed * 31 + i * 97) % Math.max(1, gameInfo.gameHeight * 0.28)),
      width: LAYOUT.ENEMY_SIZE,
      height: LAYOUT.ENEMY_SIZE,
      hp: 1,
    });
  }

  return { enemies, terrain };
}
