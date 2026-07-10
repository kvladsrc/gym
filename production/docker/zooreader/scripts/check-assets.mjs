#!/usr/bin/env node
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";

const PNG_SIGNATURE = Buffer.from([
  0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a,
]);
const LOGICAL_PIXEL_SCALE = 3;
const MAX_HERO_COLORS = 12;

const expectedAssets = [
  ["assets/player.png", 96, 96],
  ["assets/player-run.png", 384, 96, 4],
  ["assets/player-jump.png", 96, 96],
  ["assets/player-jetpack.png", 96, 96],
  ["assets/player-grenade.png", 96, 96],
  ["assets/player-flamethrower.png", 96, 96],
  ["assets/player-pistol.png", 96, 96],
  ["assets/player-knife.png", 96, 96],
  ["assets/leech-crawler.png", 512, 64],
  ["assets/gnat-flyer.png", 512, 64],
  ["assets/grenade.png", 24, 24],
  ["assets/block-desert.png", 64, 64],
  ["assets/block-grass.png", 64, 64],
  ["assets/block-snow.png", 64, 64],
];

function readPngSize(path) {
  const bytes = readFileSync(path);
  if (!bytes.subarray(0, 8).equals(PNG_SIGNATURE)) {
    throw new Error(`${path} is not a PNG file`);
  }

  const chunkType = bytes.subarray(12, 16).toString("ascii");
  if (chunkType !== "IHDR") {
    throw new Error(`${path} has no IHDR chunk at the expected offset`);
  }

  return {
    width: bytes.readUInt32BE(16),
    height: bytes.readUInt32BE(20),
  };
}

function alphaMetrics(path, width, height, offsetX = 0) {
  const output = execFileSync(
    "magick",
    [
      path,
      "-crop",
      `${width}x${height}+${offsetX}+0`,
      "+repage",
      "-format",
      `%[fx:mean.a] %[fx:p{0,0}.a] %[fx:p{${width - 1},0}.a] %[fx:p{0,${height - 1}}.a] %[fx:p{${width - 1},${height - 1}}.a]`,
      "info:",
    ],
    { encoding: "utf8" },
  );
  const values = output.trim().split(/\s+/).map(Number);
  if (values.length !== 5 || values.some((value) => !Number.isFinite(value))) {
    throw new Error(`Could not inspect alpha channel in ${path}`);
  }
  return { mean: values[0], corners: values.slice(1) };
}

function uniqueColors(path, width, height, offsetX = 0, alphaOnly = false) {
  const output = execFileSync(
    "magick",
    [
      path,
      "-crop",
      `${width}x${height}+${offsetX}+0`,
      "+repage",
      ...(alphaOnly ? ["-channel", "A", "-separate"] : []),
      "-format",
      "%k",
      "info:",
    ],
    { encoding: "utf8" },
  );
  return Number(output.trim());
}

function logicalGridError(path, width, height) {
  const logicalWidth = width / LOGICAL_PIXEL_SCALE;
  const logicalHeight = height / LOGICAL_PIXEL_SCALE;
  const output = execFileSync(
    "magick",
    [
      path,
      "(",
      "+clone",
      "-filter",
      "point",
      "-resize",
      `${logicalWidth}x${logicalHeight}!`,
      "-filter",
      "point",
      "-resize",
      `${width}x${height}!`,
      ")",
      "-compose",
      "difference",
      "-composite",
      "-format",
      "%[fx:mean]",
      "info:",
    ],
    { encoding: "utf8" },
  );
  return Number(output.trim());
}

function framePixelHash(path, width, height, offsetX) {
  const pixels = execFileSync("magick", [
    path,
    "-crop",
    `${width}x${height}+${offsetX}+0`,
    "+repage",
    "RGBA:-",
  ]);
  return createHash("sha256").update(pixels).digest("hex");
}

let failed = false;
for (const [
  path,
  expectedWidth,
  expectedHeight,
  frameCount = 1,
] of expectedAssets) {
  const { width, height } = readPngSize(path);
  if (width !== expectedWidth || height !== expectedHeight) {
    console.error(
      `${path}: expected ${expectedWidth}x${expectedHeight}, got ${width}x${height}`,
    );
    failed = true;
    continue;
  }

  const frameWidth = expectedWidth / frameCount;
  const frameHashes = [];
  for (let frameIndex = 0; frameIndex < frameCount; frameIndex += 1) {
    const { mean, corners } = alphaMetrics(
      path,
      frameWidth,
      expectedHeight,
      frameIndex * frameWidth,
    );
    const frameLabel = frameCount > 1 ? ` frame ${frameIndex + 1}` : "";
    if (mean <= 0.001) {
      console.error(`${path}:${frameLabel} image is fully transparent`);
      failed = true;
    }
    if (
      path.startsWith("assets/player") &&
      corners.some((alpha) => alpha > 0.02)
    ) {
      console.error(
        `${path}:${frameLabel} expected transparent background at all corners`,
      );
      failed = true;
    }
    if (path.startsWith("assets/player")) {
      const offsetX = frameIndex * frameWidth;
      frameHashes.push(
        framePixelHash(path, frameWidth, expectedHeight, offsetX),
      );
      const colors = uniqueColors(path, frameWidth, expectedHeight, offsetX);
      if (colors > MAX_HERO_COLORS) {
        console.error(
          `${path}:${frameLabel} expected at most ${MAX_HERO_COLORS} colors, got ${colors}`,
        );
        failed = true;
      }
      const alphaLevels = uniqueColors(
        path,
        frameWidth,
        expectedHeight,
        offsetX,
        true,
      );
      if (alphaLevels > 2) {
        console.error(
          `${path}:${frameLabel} expected hard alpha edges, got ${alphaLevels} levels`,
        );
        failed = true;
      }
    }
  }

  if (frameCount > 1 && new Set(frameHashes).size !== frameCount) {
    console.error(`${path}: animation frames must be visually distinct`);
    failed = true;
  }

  if (
    path.startsWith("assets/player") &&
    logicalGridError(path, expectedWidth, expectedHeight) !== 0
  ) {
    console.error(`${path}: pixels are not aligned to the 32x32 logical grid`);
    failed = true;
  }
}

if (failed) {
  process.exit(1);
}

console.log("Zooreader asset contract passed");
