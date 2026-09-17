#!/usr/bin/env node
// Deterministic packing of the reviewed September 2026 imagegen sources.
import { execFileSync } from "node:child_process";
import { mkdirSync } from "node:fs";
import { resolve, join } from "node:path";

const source = resolve(process.argv[2] || "asset-work/refresh-2026-09");
const output = resolve(process.argv[3] || "assets");
const work = join(source, "packed");
mkdirSync(work, { recursive: true });
mkdirSync(output, { recursive: true });
const magick = (...args) => execFileSync("magick", args.map(String));

function pack(name, crop, target, width = 96, height = 96, box = "26x26") {
  magick(
    join(source, `${name}.png`),
    "-crop",
    crop,
    "+repage",
    "-channel",
    "A",
    "-threshold",
    "50%",
    "+channel",
    "-trim",
    "+repage",
    "-filter",
    "box",
    "-resize",
    box,
    "-background",
    "none",
    "-gravity",
    "south",
    "-extent",
    `${width / 3}x${height / 3 - 3}`,
    "-gravity",
    "north",
    "-extent",
    `${width / 3}x${height / 3}`,
    "-channel",
    "A",
    "-threshold",
    "50%",
    "+channel",
    "+dither",
    "-colors",
    "12",
    "-channel",
    "A",
    "-threshold",
    "50%",
    "+channel",
    "-filter",
    "point",
    "-resize",
    `${width}x${height}!`,
    "-strip",
    `PNG32:${target}`,
  );
}

const heroNames = [
  "run-0",
  "run-1",
  "run-2",
  "run-3",
  "player",
  "player-jump",
  "player-jetpack",
  "player-grenade",
  "player-flamethrower",
  "player-pistol",
  "player-knife",
];
heroNames.forEach((name, index) => {
  pack(
    "hero-atlas",
    `362x362+${(index % 4) * 362}+${Math.floor(index / 4) * 362}`,
    join(index < 4 ? work : output, `${name}.png`),
  );
});
magick(
  ...[0, 1, 2, 3].map((i) => join(work, `run-${i}.png`)),
  "+append",
  join(output, "player-run.png"),
);
pack("hero-atlas", "362x362+1086+724", join(work, "grenade.png"));
magick(
  join(work, "grenade.png"),
  "-filter",
  "point",
  "-resize",
  "24x24!",
  join(output, "grenade.png"),
);

// Reviewed gutters: image generation does not guarantee uniform sheet cells.
const sheets = [
  ["crawler", "leech-crawler", [0, 647, 1168, 1588, 2172]],
  ["flyer", "gnat-flyer", [0, 565, 1092, 1660, 2172]],
];
for (const [name, target, edges] of sheets) {
  const frames = [];
  for (let i = 0; i < 4; i++) {
    const frame = join(work, `${name}-${i}.png`);
    magick(
      join(source, `${name}.png`),
      "-crop",
      `${edges[i + 1] - edges[i]}x724+${edges[i]}+0`,
      "+repage",
      "-channel",
      "A",
      "-threshold",
      "50%",
      "+channel",
      ...(name === "crawler" ? ["-trim", "+repage"] : []),
      "-filter",
      "box",
      "-resize",
      name === "crawler" ? "18%" : "8%",
      "-background",
      "none",
      "-gravity",
      name === "crawler" ? "south" : "center",
      "-extent",
      name === "crawler" ? "128x64" : "64x64",
      "-channel",
      "A",
      "-threshold",
      "50%",
      "+channel",
      "+dither",
      "-colors",
      "16",
      "-strip",
      `PNG32:${frame}`,
    );
    frames.push(frame);
  }
  magick(...frames, "+append", join(output, `${target}.png`));
}
for (const theme of ["grass", "snow", "desert"]) {
  magick(
    join(source, `${theme}.png`),
    ...(theme === "snow" ? ["-crop", "1254x1160+0+94", "+repage"] : []),
    "-filter",
    "box",
    "-resize",
    "32x32!",
    "+dither",
    "-colors",
    "12",
    "-filter",
    "point",
    "-resize",
    "64x64!",
    "-strip",
    join(output, `block-${theme}.png`),
  );
}
console.log(`Packed reviewed artwork into ${output}`);
