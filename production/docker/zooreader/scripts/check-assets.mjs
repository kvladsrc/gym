#!/usr/bin/env node
import { readFileSync } from "node:fs";

const PNG_SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

const expectedAssets = [
  ["assets/player.png", 96, 96],
  ["assets/player-run.png", 384, 96],
  ["assets/player-jump.png", 384, 96],
  ["assets/player-jetpack.png", 384, 96],
  ["assets/player-grenade.png", 384, 96],
  ["assets/player-flamethrower.png", 384, 96],
  ["assets/player-pistol.png", 384, 96],
  ["assets/player-knife.png", 384, 96],
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

let failed = false;
for (const [path, expectedWidth, expectedHeight] of expectedAssets) {
  const { width, height } = readPngSize(path);
  if (width !== expectedWidth || height !== expectedHeight) {
    console.error(
      `${path}: expected ${expectedWidth}x${expectedHeight}, got ${width}x${height}`,
    );
    failed = true;
  }
}

if (failed) {
  process.exit(1);
}

console.log("Zooreader asset dimensions passed");
