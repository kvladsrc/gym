#!/usr/bin/env node
/**
 * Generate a canonical ZooReader hero, static action poses, and one run sheet.
 *
 * API results stay in ignored asset-work storage. Runtime assets are replaced
 * only after every pose has been generated and validated locally.
 */
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readFileSync,
  renameSync,
  unlinkSync,
  writeFileSync,
} from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(SCRIPT_DIR, "../../../..");
const APP_DIR = resolve(
  process.env.ZOOREADER_ASSET_APP_DIR ??
    join(ROOT, "production/docker/zooreader"),
);
const WORK_DIR = join(APP_DIR, "asset-work");
const ASSETS_DIR = join(APP_DIR, "assets");
const CURRENT_HERO_PATH = join(WORK_DIR, "current-hero.json");
const API_URL = (
  process.env.OPENAI_IMAGES_API_URL ?? "https://api.openai.com/v1/images"
).replace(/\/$/, "");
const IMAGE_MODEL = process.env.OPENAI_IMAGE_MODEL ?? "gpt-image-2";
const IMAGE_QUALITY = process.env.OPENAI_IMAGE_QUALITY ?? "high";
const STATIC_SOURCE_SIZE = "1024x1024";
const RUN_SOURCE_SIZE = "2048x1024";
const TARGET_SIZE = 96;
const LOGICAL_PIXEL_SIZE = 32;
const CONTENT_BOX_SIZE = 26;
const PALETTE_COLORS = 12;
const RUN_FRAME_COUNT = 4;
const ALPHA_THRESHOLD = "50%";
const CHROMA_KEY = "#ff00ff";
const CHROMA_FUZZ = process.env.ZOOREADER_CHROMA_FUZZ ?? "18%";
const CHROMA_DESPILL =
  "a*((max(r,b)>0.04 && r>g+0.03 && b>g+0.03 && abs(r-b)<0.35)?0:1)";
const REQUEST_ATTEMPTS = Number(
  process.env.OPENAI_IMAGE_REQUEST_ATTEMPTS ?? "4",
);
const REQUEST_TIMEOUT_MS = Number(
  process.env.OPENAI_IMAGE_REQUEST_TIMEOUT_MS ?? "300000",
);
const PIPELINE_VERSION = 6;

const PNG_SIGNATURE = Buffer.from([
  0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a,
]);

const HERO_STATES = [
  { state: "idle", file: "player.png" },
  {
    state: "run",
    file: "player-run.png",
    frameCount: RUN_FRAME_COUNT,
    sourceSize: RUN_SOURCE_SIZE,
    pose: "A compact four-frame running loop toward screen-left: frame 1 left-foot contact, frame 2 passing pose, frame 3 right-foot contact, frame 4 passing pose. Use a strong boot stride, simple arm counter-motion, and a stable head silhouette.",
  },
  {
    state: "jump",
    file: "player-jump.png",
    pose: "A single compact airborne jump pose moving toward screen-left, knees raised and boots clearly separated, ready to land.",
  },
  {
    state: "jetpack",
    file: "player-jetpack.png",
    pose: "A single stable jetpack-hover pose facing screen-left, with the backpack thruster visible, legs lifted, and only a very short exhaust because the game draws its own flame.",
  },
  {
    state: "grenade",
    file: "player-grenade.png",
    pose: "A single grenade-throw action pose facing screen-left, with the throwing arm just completing its release and the other arm balancing the body. Do not show a grenade in flight, projectile, smoke, spark, or motion trail because the game draws the thrown grenade separately.",
  },
  {
    state: "flamethrower",
    file: "player-flamethrower.png",
    pose: "A single braced flamethrower-firing pose facing screen-left, both hands controlling a compact nozzle; show no emitted flame because the game draws the attack effect.",
  },
  {
    state: "pistol",
    file: "player-pistol.png",
    pose: "A single pistol-firing pose facing screen-left, arm extended with restrained recoil and the silhouette still balanced over the boots.",
  },
  {
    state: "knife",
    file: "player-knife.png",
    pose: "A single side-on knife-slash pose facing screen-left, torso twisted but face and feet still oriented left; show no motion arc because the game draws it.",
  },
];

const HERO_STYLE = `Use case: stylized-concept
Asset type: tiny 2D game character drawn at roughly 60x60 screen pixels
Primary request: create an original, charming retro action hero with the broad
readability of a cozy 16-bit adventure-RPG character, without copying any
existing game or character.
Subject: slightly oversized square head, one blocky blond flat-top shape,
black sunglasses as one bold band, broad red sleeveless torso, compact dark
trousers, oversized black gloves, and large heavy boots. Keep the anatomy
short, chunky, and deliberately stylized rather than realistic.
Style/medium: hand-authored pixel art designed on a 32x32 logical grid and
upscaled. Use 8-12 flat colors total, a strong dark outline, large color
clusters, at most two shades per material, and a clear silhouette.
Constraints: every important feature must survive at 60x60. Avoid individual
bullets, boot laces, muscle striations, fabric folds, hatching, dithering,
realistic anatomy, gradients, antialiasing, thin lines, and any detail smaller
than two logical pixels. No text, logo, interface, trademark, watermark, or
exact likeness of a protected character.`;

const COMPOSITION = `The source canvas is square. Show exactly one complete
full-body character in exactly one pose. This is one static game image, not
an animation, sprite sheet, sequence, lineup, turn-around, or multi-panel
composition. The character faces screen-left. Keep the same camera, apparent
body scale, horizontal center, and generous outer padding as the supplied
reference. Do not crop hair, weapon, hands, or boots. Use a perfectly uniform
opaque ${CHROMA_KEY} magenta background across the entire canvas, without a
ground plane, shadow, texture, gradient, border, or scenery.`;

const RUN_COMPOSITION = `Create one landscape source image containing exactly
four full-body poses of the same hero in one left-to-right row. Treat the canvas
as four equal 512x1024 columns. Center exactly one pose inside each column at
the same apparent scale and baseline. Preserve the same face, hair, sunglasses,
outfit, palette, outline, body proportions, and screen-left orientation across
all four columns. Do not add labels, borders, grid lines, panels, extra poses,
effects, or characters. Keep every pose fully inside its column. Use one
perfectly uniform opaque ${CHROMA_KEY} magenta background across the entire
canvas, without a ground plane, shadow, texture, gradient, or scenery.`;

function fail(message) {
  throw new Error(message);
}

function requireId(value, label) {
  if (typeof value !== "string" || !/^[a-z0-9][a-z0-9-]*$/i.test(value)) {
    fail(`${label} must contain only letters, digits, and hyphens`);
  }
  return value;
}

function timestamp() {
  return new Date().toISOString().replace(/[:.]/g, "-");
}

function readJson(path) {
  return JSON.parse(readFileSync(path, "utf8"));
}

function writeJson(path, data) {
  mkdirSync(dirname(path), { recursive: true });
  const temporary = `${path}.tmp-${process.pid}`;
  writeFileSync(temporary, `${JSON.stringify(data, null, 2)}\n`);
  renameSync(temporary, path);
}

function manifestPath(path) {
  return relative(APP_DIR, path);
}

function runMagick(args, options = {}) {
  return execFileSync("magick", args, {
    stdio: options.capture ? "pipe" : "inherit",
    encoding: options.capture ? "utf8" : undefined,
  });
}

function requireMagick() {
  try {
    execFileSync("magick", ["-version"], { stdio: "ignore" });
  } catch {
    fail(
      "ImageMagick is required; run through: nix develop -c just zooreader <recipe>",
    );
  }
}

function requireKey() {
  if (!process.env.OPENAI_API_KEY) {
    fail("OPENAI_API_KEY is required; export it before generating hero assets");
  }
}

function validateConfiguration() {
  if (!Number.isInteger(REQUEST_ATTEMPTS) || REQUEST_ATTEMPTS < 1) {
    fail("OPENAI_IMAGE_REQUEST_ATTEMPTS must be a positive integer");
  }
  if (!new Set(["auto", "low", "medium", "high"]).has(IMAGE_QUALITY)) {
    fail("OPENAI_IMAGE_QUALITY must be auto, low, medium, or high");
  }
  if (!Number.isInteger(REQUEST_TIMEOUT_MS) || REQUEST_TIMEOUT_MS < 1000) {
    fail("OPENAI_IMAGE_REQUEST_TIMEOUT_MS must be at least 1000");
  }
  if (/^gpt-image-1(?:\.|-|$)/.test(IMAGE_MODEL)) {
    fail("GPT Image 1.x is not supported by this workflow; use gpt-image-2");
  }
}

function sleep(milliseconds) {
  return new Promise((resolvePromise) => {
    setTimeout(resolvePromise, milliseconds);
  });
}

function retryDelay(response, attempt) {
  const retryAfter = response.headers.get("retry-after");
  if (retryAfter) {
    const seconds = Number(retryAfter);
    if (Number.isFinite(seconds) && seconds >= 0) return seconds * 1000;

    const retryAt = Date.parse(retryAfter);
    if (Number.isFinite(retryAt)) return Math.max(0, retryAt - Date.now());
  }
  return 1000 * 2 ** (attempt - 1);
}

function errorDetails(body, fallback) {
  return body?.error?.message ?? body?.error ?? fallback;
}

async function requestJson(path, options, label) {
  for (let attempt = 1; attempt <= REQUEST_ATTEMPTS; attempt += 1) {
    let response;
    let responseText;
    try {
      response = await fetch(`${API_URL}/${path}`, {
        ...options,
        signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
      });
      responseText = await response.text();
    } catch (error) {
      if (attempt === REQUEST_ATTEMPTS) {
        fail(`${label} failed: ${error.message}`);
      }
      const delay = 1000 * 2 ** (attempt - 1);
      console.warn(
        `${label} network attempt ${attempt}/${REQUEST_ATTEMPTS} failed; retrying in ${delay / 1000}s`,
      );
      await sleep(delay);
      continue;
    }

    let body;
    try {
      body = JSON.parse(responseText);
    } catch {
      body = null;
    }

    if (response.ok) {
      if (!body) fail(`${label} returned invalid JSON`);
      return body;
    }

    const retryable = response.status === 429 || response.status >= 500;
    const details = errorDetails(body, responseText || response.statusText);
    if (!retryable || attempt === REQUEST_ATTEMPTS) {
      fail(`${label} failed (${response.status}): ${details}`);
    }

    const delay = retryDelay(response, attempt);
    console.warn(
      `${label} attempt ${attempt}/${REQUEST_ATTEMPTS} failed (${response.status}); retrying in ${Math.ceil(delay / 1000)}s`,
    );
    await sleep(delay);
  }
  fail(`${label} failed after ${REQUEST_ATTEMPTS} attempts`);
}

function imageFromResponse(body, label) {
  const encoded = body?.data?.[0]?.b64_json;
  if (typeof encoded !== "string" || encoded.length === 0) {
    fail(`${label} returned no PNG data`);
  }
  return Buffer.from(encoded, "base64");
}

async function generateImage(prompt) {
  requireKey();
  const body = await requestJson(
    "generations",
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${process.env.OPENAI_API_KEY}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        model: IMAGE_MODEL,
        prompt,
        n: 1,
        size: STATIC_SOURCE_SIZE,
        quality: IMAGE_QUALITY,
        background: "opaque",
        output_format: "png",
      }),
    },
    "OpenAI concept generation",
  );
  return { png: imageFromResponse(body, "OpenAI concept generation"), body };
}

async function editImage(referencePath, prompt, state, sourceSize) {
  requireKey();
  const form = new FormData();
  form.set("model", IMAGE_MODEL);
  form.set("prompt", prompt);
  form.set("size", sourceSize);
  form.set("quality", IMAGE_QUALITY);
  form.set("background", "opaque");
  form.set("output_format", "png");
  form.append(
    "image[]",
    new Blob([readFileSync(referencePath)], { type: "image/png" }),
    "hero-reference.png",
  );
  const label = `OpenAI ${state} pose edit`;
  const body = await requestJson(
    "edits",
    {
      method: "POST",
      headers: { Authorization: `Bearer ${process.env.OPENAI_API_KEY}` },
      body: form,
    },
    label,
  );
  return { png: imageFromResponse(body, label), body };
}

function readPngSize(path) {
  const bytes = readFileSync(path);
  if (bytes.length < 24 || !bytes.subarray(0, 8).equals(PNG_SIGNATURE)) {
    fail(`${path} is not a PNG file`);
  }
  if (bytes.subarray(12, 16).toString("ascii") !== "IHDR") {
    fail(`${path} has no IHDR chunk at the expected offset`);
  }
  return {
    width: bytes.readUInt32BE(16),
    height: bytes.readUInt32BE(20),
  };
}

function sha256(path) {
  return createHash("sha256").update(readFileSync(path)).digest("hex");
}

function stateFingerprint(referenceHash, spec) {
  return createHash("sha256")
    .update(
      JSON.stringify({
        pipelineVersion: PIPELINE_VERSION,
        sourceFingerprint: stateSourceFingerprint(referenceHash, spec),
        targetSize: TARGET_SIZE,
        logicalPixelSize: LOGICAL_PIXEL_SIZE,
        contentBoxSize: CONTENT_BOX_SIZE,
        paletteColors: PALETTE_COLORS,
        frameCount: stateFrameCount(spec),
        chromaKey: CHROMA_KEY,
        chromaFuzz: CHROMA_FUZZ,
        chromaDespill: CHROMA_DESPILL,
        alphaThreshold: ALPHA_THRESHOLD,
      }),
    )
    .digest("hex");
}

function stateSourceFingerprint(referenceHash, spec) {
  return createHash("sha256")
    .update(
      JSON.stringify({
        model: IMAGE_MODEL,
        quality: IMAGE_QUALITY,
        sourceSize: stateSourceSize(spec),
        referenceHash,
        state: spec.state,
        prompt: spec.pose ? statePrompt(spec) : conceptPrompt(),
      }),
    )
    .digest("hex");
}

function stateFrameCount(spec) {
  return spec.frameCount ?? 1;
}

function stateSourceSize(spec) {
  return spec.sourceSize ?? STATIC_SOURCE_SIZE;
}

function alphaMetrics(path, frameIndex) {
  const offset = frameIndex * TARGET_SIZE;
  const output = runMagick(
    [
      path,
      "-crop",
      `${TARGET_SIZE}x${TARGET_SIZE}+${offset}+0`,
      "+repage",
      "-format",
      "%[fx:mean.a] %[fx:p{0,0}.a] %[fx:p{95,0}.a] %[fx:p{0,95}.a] %[fx:p{95,95}.a]",
      "info:",
    ],
    { capture: true },
  );
  const values = output.trim().split(/\s+/).map(Number);
  if (values.length !== 5 || values.some((value) => !Number.isFinite(value))) {
    fail(`Could not inspect alpha channel in ${path}`);
  }
  return { mean: values[0], corners: values.slice(1) };
}

function framePixelHash(path, frameIndex) {
  const offset = frameIndex * TARGET_SIZE;
  const pixels = execFileSync(
    "magick",
    [
      path,
      "-crop",
      `${TARGET_SIZE}x${TARGET_SIZE}+${offset}+0`,
      "+repage",
      "RGBA:-",
    ],
    { stdio: ["ignore", "pipe", "inherit"] },
  );
  return createHash("sha256").update(pixels).digest("hex");
}

function validateFrames(path, frameCount = 1) {
  const { width, height } = readPngSize(path);
  const expectedWidth = TARGET_SIZE * frameCount;
  if (width !== expectedWidth || height !== TARGET_SIZE) {
    fail(
      `${path}: expected ${expectedWidth}x${TARGET_SIZE}, got ${width}x${height}`,
    );
  }

  for (let frameIndex = 0; frameIndex < frameCount; frameIndex += 1) {
    const { mean, corners } = alphaMetrics(path, frameIndex);
    if (mean <= 0.001) {
      fail(`${path}: frame ${frameIndex + 1} is fully transparent`);
    }
    if (corners.some((alpha) => alpha > 0.02)) {
      fail(`${path}: frame ${frameIndex + 1} retains background at a corner`);
    }
  }

  if (frameCount > 1) {
    const hashes = Array.from({ length: frameCount }, (_, frameIndex) =>
      framePixelHash(path, frameIndex),
    );
    if (new Set(hashes).size !== frameCount) {
      fail(`${path}: animation frames must be visually distinct`);
    }
  }
}

function validFrames(path, frameCount = 1) {
  if (!existsSync(path)) return false;
  try {
    validateFrames(path, frameCount);
    return true;
  } catch {
    return false;
  }
}

function validSourceSize(path, spec) {
  if (!existsSync(path)) return false;
  const [expectedWidth, expectedHeight] = stateSourceSize(spec)
    .split("x")
    .map(Number);
  try {
    const { width, height } = readPngSize(path);
    return width === expectedWidth && height === expectedHeight;
  } catch {
    return false;
  }
}

function normalizeFrame(source, target, crop = null) {
  mkdirSync(dirname(target), { recursive: true });
  runMagick([
    source,
    ...(crop
      ? [
          "-crop",
          `${crop.width}x${crop.height}+${crop.x}+${crop.y}`,
          "+repage",
          "-background",
          CHROMA_KEY,
          "-gravity",
          "center",
          "-extent",
          `${crop.height}x${crop.height}`,
        ]
      : []),
    "-alpha",
    "on",
    "-fuzz",
    CHROMA_FUZZ,
    "-transparent",
    CHROMA_KEY,
    "-channel",
    "A",
    "-fx",
    CHROMA_DESPILL,
    "+channel",
    "-trim",
    "+repage",
    "-filter",
    "box",
    "-resize",
    `${CONTENT_BOX_SIZE}x${CONTENT_BOX_SIZE}`,
    "-background",
    "none",
    "-gravity",
    "center",
    "-extent",
    `${LOGICAL_PIXEL_SIZE}x${LOGICAL_PIXEL_SIZE}`,
    "-channel",
    "A",
    "-threshold",
    ALPHA_THRESHOLD,
    "+channel",
    "-channel",
    "RGB",
    "+dither",
    "-colors",
    String(PALETTE_COLORS),
    "+channel",
    "-filter",
    "point",
    "-resize",
    `${TARGET_SIZE}x${TARGET_SIZE}!`,
    "-strip",
    `PNG32:${target}`,
  ]);
  validateFrames(target);
}

function normalizeSource(source, target) {
  normalizeFrame(source, target);
}

function normalizeStateSource(spec, source, output, statesDir) {
  const frameCount = stateFrameCount(spec);
  if (frameCount === 1) {
    normalizeSource(source, output);
    return;
  }

  const [sourceWidth, sourceHeight] = stateSourceSize(spec)
    .split("x")
    .map(Number);
  const sourceFrameWidth = sourceWidth / frameCount;
  if (!Number.isInteger(sourceFrameWidth)) {
    fail(`${spec.state}: source width must divide evenly into frames`);
  }

  const frames = [];
  for (let frameIndex = 0; frameIndex < frameCount; frameIndex += 1) {
    const frame = join(
      statesDir,
      `${spec.state}-frame-${String(frameIndex + 1).padStart(2, "0")}.png`,
    );
    normalizeFrame(source, frame, {
      width: sourceFrameWidth,
      height: sourceHeight,
      x: sourceFrameWidth * frameIndex,
      y: 0,
    });
    frames.push(frame);
  }

  runMagick([...frames, "+append", `PNG32:${output}`]);
  validateFrames(output, frameCount);
}

function createAnimationPreview(sheet, target) {
  runMagick([
    sheet,
    "-crop",
    `${TARGET_SIZE}x${TARGET_SIZE}`,
    "+repage",
    "-set",
    "delay",
    "12",
    "-loop",
    "0",
    target,
  ]);
}

function createContactSheet(paths, target, tile) {
  runMagick([
    "montage",
    ...paths,
    "-thumbnail",
    "192x192",
    "-background",
    "#c8c8c8",
    "-tile",
    tile,
    "-geometry",
    "+12+28",
    "-pointsize",
    "18",
    "-set",
    "label",
    "%t",
    target,
  ]);
}

function conceptPrompt() {
  return `${HERO_STYLE}\n\nCreate the canonical identity reference in a relaxed, combat-ready idle stance. The compact blocky pistol is lowered but visible. Preserve a clean side-on screen-left read. Let the character occupy about 80 percent of the canvas height.\n\n${COMPOSITION}`;
}

function posePrompt(pose) {
  return `${HERO_STYLE}\n\nUse the supplied image as the canonical identity reference. This must be the same hero, not a redesign. Preserve the face, haircut, sunglasses, body proportions, clothing construction, palette, outline weight, pixel clusters, camera, and screen-left orientation. Change only the pose and the state-specific equipment required below.\n\nState pose: ${pose}\n\n${COMPOSITION}`;
}

function runPrompt(spec) {
  return `${HERO_STYLE}\n\nUse the supplied image as the canonical identity reference. This must be the same hero in every frame, not four redesigns. Preserve the face, haircut, sunglasses, body proportions, clothing construction, palette, outline weight, camera, and screen-left orientation. Simplify details consistently across every pose.\n\nAnimation: ${spec.pose}\n\n${RUN_COMPOSITION}`;
}

function statePrompt(spec) {
  return stateFrameCount(spec) > 1 ? runPrompt(spec) : posePrompt(spec.pose);
}

function conceptSource(conceptId) {
  const conceptDir = join(WORK_DIR, "concepts", conceptId);
  const heroReference = join(WORK_DIR, "heroes", conceptId, "reference.png");
  const candidates = [
    heroReference,
    join(conceptDir, "source.png"),
    join(conceptDir, "variant-01.png"),
  ];
  const source = candidates.find((candidate) => existsSync(candidate));
  if (!source) fail(`Active hero reference is missing for ${conceptId}`);
  return source;
}

function loadActiveConcept() {
  if (!existsSync(CURRENT_HERO_PATH)) {
    fail("No active hero concept; run just zooreader asset-hero-concept first");
  }
  const active = readJson(CURRENT_HERO_PATH);
  const concept = requireId(active.concept, "active hero concept");
  return { concept, reference: conceptSource(concept) };
}

async function heroConcept() {
  requireMagick();
  requireKey();
  validateConfiguration();

  const id = `hero-${timestamp()}`;
  const conceptDir = join(WORK_DIR, "concepts", id);
  const heroDir = join(WORK_DIR, "heroes", id);
  const source = join(conceptDir, "source.png");
  const preview = join(conceptDir, "concept.png");
  const contactSheet = join(conceptDir, "contact-sheet.png");
  const prompt = conceptPrompt();

  mkdirSync(conceptDir, { recursive: true });
  const response = await generateImage(prompt);
  writeFileSync(source, response.png);
  normalizeSource(source, preview);
  createContactSheet([preview], contactSheet, "1x1");

  mkdirSync(heroDir, { recursive: true });
  copyFileSync(source, join(heroDir, "reference.png"));
  writeJson(join(conceptDir, "manifest.json"), {
    schemaVersion: PIPELINE_VERSION,
    id,
    model: IMAGE_MODEL,
    quality: IMAGE_QUALITY,
    sourceSize: STATIC_SOURCE_SIZE,
    targetSize: TARGET_SIZE,
    logicalPixelSize: LOGICAL_PIXEL_SIZE,
    contentBoxSize: CONTENT_BOX_SIZE,
    paletteColors: PALETTE_COLORS,
    prompt,
    source: manifestPath(source),
    preview: manifestPath(preview),
    usage: response.body.usage ?? null,
  });
  writeJson(join(heroDir, "manifest.json"), {
    schemaVersion: PIPELINE_VERSION,
    concept: id,
    reference: manifestPath(join(heroDir, "reference.png")),
  });
  writeJson(CURRENT_HERO_PATH, {
    schemaVersion: PIPELINE_VERSION,
    concept: id,
  });

  console.log(`Hero concept created: ${id}`);
  console.log(`Review full source: ${source}`);
  console.log(`Review 96x96 concept: ${contactSheet}`);
  console.log("If accepted, run: just zooreader asset-hero-full");
}

function fullManifest(concept, reference, referenceHash, states = {}) {
  return {
    schemaVersion: PIPELINE_VERSION,
    concept,
    model: IMAGE_MODEL,
    quality: IMAGE_QUALITY,
    staticSourceSize: STATIC_SOURCE_SIZE,
    runSourceSize: RUN_SOURCE_SIZE,
    targetSize: TARGET_SIZE,
    logicalPixelSize: LOGICAL_PIXEL_SIZE,
    contentBoxSize: CONTENT_BOX_SIZE,
    paletteColors: PALETTE_COLORS,
    reference: manifestPath(reference),
    referenceSha256: referenceHash,
    states,
  };
}

async function ensureActionPose(
  spec,
  reference,
  referenceHash,
  statesDir,
  manifest,
) {
  const source = join(statesDir, `${spec.state}-source.png`);
  const output = join(statesDir, spec.file);
  const prompt = statePrompt(spec);
  const fingerprint = stateFingerprint(referenceHash, spec);
  const sourceFingerprint = stateSourceFingerprint(referenceHash, spec);
  const previous = manifest.states[spec.state];
  const frameCount = stateFrameCount(spec);

  if (
    previous?.fingerprint === fingerprint &&
    validFrames(output, frameCount)
  ) {
    console.log(`Reusing ${spec.state} pose: ${output}`);
    return output;
  }

  const reusableSource =
    validSourceSize(source, spec) &&
    (previous?.sourceFingerprint === sourceFingerprint ||
      (previous?.prompt === prompt &&
        previous?.sourceSize === stateSourceSize(spec)));
  if (reusableSource) {
    try {
      normalizeStateSource(spec, source, output, statesDir);
      console.log(`Recovered ${spec.state} pose from saved API source`);
      manifest.states[spec.state] = {
        ...previous,
        fingerprint,
        sourceFingerprint,
        frameCount,
        sourceSize: stateSourceSize(spec),
        prompt,
        source: manifestPath(source),
        output: manifestPath(output),
      };
      return output;
    } catch (error) {
      console.warn(`Saved ${spec.state} source is unusable: ${error.message}`);
    }
  }

  console.log(
    `Generating ${spec.state} pose with ${IMAGE_MODEL} (${IMAGE_QUALITY})`,
  );
  const response = await editImage(
    reference,
    prompt,
    spec.state,
    stateSourceSize(spec),
  );
  writeFileSync(source, response.png);
  normalizeStateSource(spec, source, output, statesDir);
  manifest.states[spec.state] = {
    fingerprint,
    sourceFingerprint,
    frameCount,
    sourceSize: stateSourceSize(spec),
    prompt,
    source: manifestPath(source),
    output: manifestPath(output),
    usage: response.body.usage ?? null,
  };
  return output;
}

function installHeroAssets(outputs) {
  for (const { source, spec } of outputs) {
    validateFrames(source, stateFrameCount(spec));
  }
  mkdirSync(ASSETS_DIR, { recursive: true });
  const prepared = [];
  try {
    for (const { source, file, spec } of outputs) {
      const target = join(ASSETS_DIR, file);
      const temporary = `${target}.next-${process.pid}`;
      copyFileSync(source, temporary);
      prepared.push({
        target,
        temporary,
        previous: existsSync(target) ? readFileSync(target) : null,
      });
      validateFrames(temporary, stateFrameCount(spec));
    }
  } catch (error) {
    for (const { temporary } of prepared) {
      if (existsSync(temporary)) unlinkSync(temporary);
    }
    throw error;
  }

  try {
    for (const { target, temporary } of prepared) {
      renameSync(temporary, target);
      console.log(`Installed ${target}`);
    }
  } catch (error) {
    for (const { target, previous } of prepared) {
      if (previous) {
        writeFileSync(target, previous);
      } else if (existsSync(target)) {
        unlinkSync(target);
      }
    }
    throw error;
  } finally {
    for (const { temporary } of prepared) {
      if (existsSync(temporary)) unlinkSync(temporary);
    }
  }
}

async function heroFull() {
  requireMagick();
  validateConfiguration();
  const { concept, reference } = loadActiveConcept();
  const heroDir = join(WORK_DIR, "heroes", concept);
  const statesDir = join(heroDir, "states");
  const manifestFile = join(heroDir, "full-manifest.json");
  const referenceHash = sha256(reference);
  mkdirSync(statesDir, { recursive: true });

  let manifest = fullManifest(concept, reference, referenceHash);
  if (existsSync(manifestFile)) {
    try {
      const previous = readJson(manifestFile);
      if (
        previous.concept === concept &&
        previous.states &&
        typeof previous.states === "object"
      ) {
        manifest = fullManifest(
          concept,
          reference,
          referenceHash,
          previous.states,
        );
      }
    } catch (error) {
      console.warn(`Ignoring incomplete full manifest: ${error.message}`);
    }
  }

  const idleSpec = HERO_STATES[0];
  const idleOutput = join(statesDir, idleSpec.file);
  const idleFingerprint = stateFingerprint(referenceHash, idleSpec);
  const idleSourceFingerprint = stateSourceFingerprint(referenceHash, idleSpec);
  if (
    manifest.states.idle?.fingerprint !== idleFingerprint ||
    !validFrames(idleOutput)
  ) {
    normalizeSource(reference, idleOutput);
  }
  manifest.states.idle = {
    fingerprint: idleFingerprint,
    sourceFingerprint: idleSourceFingerprint,
    frameCount: stateFrameCount(idleSpec),
    sourceSize: stateSourceSize(idleSpec),
    source: manifestPath(reference),
    output: manifestPath(idleOutput),
  };
  writeJson(manifestFile, manifest);

  const outputs = [{ source: idleOutput, file: idleSpec.file, spec: idleSpec }];
  for (const spec of HERO_STATES.slice(1)) {
    const output = await ensureActionPose(
      spec,
      reference,
      referenceHash,
      statesDir,
      manifest,
    );
    outputs.push({ source: output, file: spec.file, spec });
    if (!manifest.states[spec.state]) {
      manifest.states[spec.state] = {
        fingerprint: stateFingerprint(referenceHash, spec),
        sourceFingerprint: stateSourceFingerprint(referenceHash, spec),
        frameCount: stateFrameCount(spec),
        sourceSize: stateSourceSize(spec),
        prompt: statePrompt(spec),
        source: manifestPath(join(statesDir, `${spec.state}-source.png`)),
        output: manifestPath(output),
        usage: null,
      };
    }
    writeJson(manifestFile, manifest);
  }

  const runOutput = join(statesDir, HERO_STATES[1].file);
  const runPreview = join(heroDir, "run-preview.gif");
  createAnimationPreview(runOutput, runPreview);

  const contactSheet = join(heroDir, "contact-sheet.png");
  createContactSheet(
    outputs.map(({ source, spec }) =>
      spec.state === "run" ? join(statesDir, "run-frame-01.png") : source,
    ),
    contactSheet,
    "4x2",
  );
  installHeroAssets(outputs);

  console.log(`Full hero contact sheet: ${contactSheet}`);
  console.log(`Run animation preview: ${runPreview}`);
  console.log("Hero assets installed. Run: just zooreader check");
}

const [command] = process.argv.slice(2);
try {
  switch (command) {
    case "hero-concept":
      await heroConcept();
      break;
    case "hero-full":
      await heroFull();
      break;
    default:
      fail("Usage: assets.mjs <hero-concept|hero-full>");
  }
} catch (error) {
  console.error(`Asset workflow failed: ${error.message}`);
  process.exitCode = 1;
}
