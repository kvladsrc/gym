// Draws a mesh's thumbnail (ADR-011) away from the page: one model at a time,
// in a WebGL context of its own. Lit and exposed as the viewer shows it
// (model-viewer's neutral environment at its default exposure), seen from the
// viewer's starting angle and cut to the model's outline.
import EnvironmentScene from "@google/model-viewer/lib/three-components/EnvironmentScene.js";
import {
  Box3,
  InstancedMesh,
  Line,
  Mesh,
  MeshStandardMaterial,
  NeutralToneMapping,
  PMREMGenerator,
  PerspectiveCamera,
  Points,
  Scene,
  SkinnedMesh,
  Texture,
  Vector3,
  WebGLRenderer,
} from "three";
import type { Material, Object3D } from "three";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";

import { CLAY } from "./clay";

export interface PreviewRequest {
  id: string;
  /** The model's GLB. */
  url: string;
  /** Paint it as clay: the model has no colour of its own. */
  clay: boolean;
  /** The picture's longer side, pixels. */
  size: number;
}

export type PreviewReply =
  | { id: string; picture: Blob }
  /** fatal: this browser cannot draw at all, whatever the model. */
  | { id: string; error: string; fatal: boolean };

/** model-viewer's exposure in its neutral environment: exposure 1 × its
 * COMMERCE_EXPOSURE with neutral tone mapping. */
const EXPOSURE = 1.3;
/** model-viewer blurs its generated environment by this much, radians. */
const ENVIRONMENT_SIGMA = 0.04;
/** The viewer's starting orbit "0deg 75deg": front-on (glTF's +Z), from 15° above. */
const ELEVATION = (15 * Math.PI) / 180;
const VIEW = new Vector3(0, Math.sin(ELEVATION), Math.cos(ELEVATION));
/** Vertical field of view, degrees: little perspective, like the viewer's. */
const FOV = 30;
/** Space around the model on each side, as a share of its longer extent. */
const MARGIN = 0.04;
/** A picture of a long model is at most this many times longer than wide. */
const MAX_ASPECT = 4;
/** Vertices of a posed (skinned or morphed) mesh sampled to frame it. */
const POSED_SAMPLES = 50_000;

interface Tools {
  canvas: OffscreenCanvas;
  renderer: WebGLRenderer;
  environment: Texture;
}

let tools: Tools | null = null;

/** The renderer and its light, made once (and again after a lost context). */
function setUp(): Tools {
  if (tools?.renderer.getContext().isContextLost()) {
    tools.renderer.dispose();
    tools = null;
  }
  if (tools) return tools;
  const canvas = new OffscreenCanvas(1, 1);
  const renderer = new WebGLRenderer({
    canvas,
    antialias: true,
    alpha: true,
    preserveDrawingBuffer: true,
  });
  renderer.setClearColor(0x000000, 0);
  renderer.toneMapping = NeutralToneMapping;
  renderer.toneMappingExposure = EXPOSURE;
  const room = new EnvironmentScene("neutral");
  const generator = new PMREMGenerator(renderer);
  const environment = generator.fromScene(room, ENVIRONMENT_SIGMA).texture;
  generator.dispose();
  release(room);
  tools = { canvas, renderer, environment };
  return tools;
}

type Drawable = Mesh | Points | Line;

/** Has geometry. (A plain `instanceof` leaves the geometry's type unknown.) */
const isDrawable = (node: Object3D): node is Drawable =>
  node instanceof Mesh || node instanceof Points || node instanceof Line;

const materialsOf = (node: Drawable): Material[] =>
  Array.isArray(node.material) ? node.material : [node.material];

/** The viewer's look for a shape without colour (see clay.ts). */
function paintClay(model: Object3D) {
  model.traverse((node) => {
    if (!(node instanceof Mesh)) return;
    for (const material of materialsOf(node)) {
      if (!(material instanceof MeshStandardMaterial)) continue;
      material.color.setRGB(...CLAY.color);
      material.metalness = CLAY.metallic;
      material.roughness = CLAY.roughness;
    }
  });
}

function corners({ min, max }: Box3): Vector3[] {
  return [0, 1, 2, 3, 4, 5, 6, 7].map(
    (bits) =>
      new Vector3(
        bits & 1 ? max.x : min.x,
        bits & 2 ? max.y : min.y,
        bits & 4 ? max.z : min.z,
      ),
  );
}

/** Calls `visit` with points of everything visible, in world space: every
 * vertex of a still mesh, a sample of a posed one as posed, the corners of
 * the box around instanced copies. The point is reused between calls. */
function eachPoint(model: Object3D, visit: (point: Vector3) => void) {
  const point = new Vector3();
  model.traverseVisible((node) => {
    if (node instanceof InstancedMesh) {
      node.computeBoundingBox();
      if (node.boundingBox && !node.boundingBox.isEmpty())
        for (const corner of corners(node.boundingBox))
          visit(corner.applyMatrix4(node.matrixWorld));
      return;
    }
    if (!isDrawable(node)) return;
    const position = node.geometry.attributes.position;
    if (!position) return;
    const posed =
      node instanceof Mesh &&
      (node instanceof SkinnedMesh ||
        (node.morphTargetInfluences ?? []).some((weight) => weight !== 0))
        ? node
        : null;
    const step = posed
      ? Math.max(1, Math.floor(position.count / POSED_SAMPLES))
      : 1;
    for (let index = 0; index < position.count; index += step) {
      if (posed) posed.getVertexPosition(index, point);
      else point.fromBufferAttribute(position, index);
      visit(point.applyMatrix4(node.matrixWorld));
    }
  });
}

/** A camera that sees the model from the viewer's angle, cut to its outline
 * plus a margin, and the picture's size in pixels. */
function frame(model: Object3D, size: number) {
  const box = new Box3();
  eachPoint(model, (point) => box.expandByPoint(point));
  const center = box.getCenter(new Vector3());
  const radius = box.getSize(new Vector3()).length() / 2;
  if (box.isEmpty() || !Number.isFinite(radius) || radius <= 0)
    throw new Error("the model has no extent");
  // Far enough for the bounding sphere to fit the view in any direction.
  const distance = radius / Math.sin((FOV * Math.PI) / 360);
  const camera = new PerspectiveCamera(
    FOV,
    1,
    distance - radius * 1.01,
    distance + radius * 1.01,
  );
  camera.position.copy(VIEW).multiplyScalar(distance).add(center);
  camera.lookAt(center);
  camera.updateMatrixWorld();

  // The outline on the screen, in normalised device coordinates (y up).
  let left = Infinity;
  let right = -Infinity;
  let bottom = Infinity;
  let top = -Infinity;
  eachPoint(model, (point) => {
    point.project(camera);
    left = Math.min(left, point.x);
    right = Math.max(right, point.x);
    bottom = Math.min(bottom, point.y);
    top = Math.max(top, point.y);
  });
  const pad = Math.max(right - left, top - bottom) * MARGIN;
  let width = right - left + 2 * pad;
  let height = top - bottom + 2 * pad;
  width = Math.max(width, height / MAX_ASPECT);
  height = Math.max(height, width / MAX_ASPECT);
  const across = Math.round(
    (size * Math.min(width, height)) / Math.max(width, height),
  );
  const [pixelsWide, pixelsHigh] =
    width >= height ? [size, across] : [across, size];
  // The cut takes the pixels' proportions exactly, around the outline's middle.
  if (width >= height) height = (width * pixelsHigh) / pixelsWide;
  else width = (height * pixelsWide) / pixelsHigh;
  const x = (left + right) / 2;
  const y = (bottom + top) / 2;
  // The whole view is 2 × 2; offsets count from its top left corner.
  camera.setViewOffset(
    2,
    2,
    x - width / 2 + 1,
    1 - (y + height / 2),
    width,
    height,
  );
  return { camera, pixelsWide, pixelsHigh };
}

/** Whether any pixel of the picture is not transparent. */
function drewSomething(renderer: WebGLRenderer, width: number, height: number) {
  const gl = renderer.getContext();
  const pixels = new Uint8Array(width * height * 4);
  gl.readPixels(0, 0, width, height, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
  for (let index = 3; index < pixels.length; index += 4)
    if (pixels[index] !== 0) return true;
  return false;
}

/** Frees the GPU memory and decoded images of a model. */
function release(root: Object3D) {
  const textures = new Set<Texture>();
  root.traverse((node) => {
    if (isDrawable(node)) {
      node.geometry.dispose();
      for (const material of materialsOf(node)) {
        for (const value of Object.values(material))
          if (value instanceof Texture) textures.add(value);
        material.dispose();
      }
    }
    if (node instanceof SkinnedMesh) node.skeleton.dispose();
  });
  for (const texture of textures) {
    texture.dispose();
    const image: unknown = texture.source.data;
    if (image instanceof ImageBitmap) image.close();
  }
}

async function draw(
  { url, clay, size }: PreviewRequest,
  { canvas, renderer, environment }: Tools,
): Promise<Blob> {
  const response = await fetch(url);
  if (!response.ok)
    throw new Error(`the model did not load: HTTP ${response.status}`);
  const gltf = await new GLTFLoader().parseAsync(
    await response.arrayBuffer(),
    "",
  );
  // A file without scenes has none (despite the type).
  const model = gltf.scene as Object3D | undefined;
  if (!model) throw new Error("the file has no scene");
  const scene = new Scene();
  scene.environment = environment;
  scene.add(model);
  try {
    scene.updateMatrixWorld(true);
    // Framed whole: nothing to cull, and no bounds of skinned meshes to compute.
    model.traverse((node) => {
      node.frustumCulled = false;
    });
    if (clay) paintClay(model);
    const { camera, pixelsWide, pixelsHigh } = frame(model, size);
    renderer.setSize(pixelsWide, pixelsHigh, false);
    renderer.render(scene, camera);
    if (!drewSomething(renderer, pixelsWide, pixelsHigh))
      throw new Error("the model drew nothing");
    return await canvas.convertToBlob({ type: "image/png" });
  } finally {
    scene.remove(model);
    release(model);
    renderer.renderLists.dispose();
  }
}

const describe = (error: unknown) =>
  error instanceof Error ? error.message : String(error);

async function answer(request: PreviewRequest): Promise<PreviewReply> {
  let ready: Tools;
  try {
    ready = setUp();
  } catch (error) {
    return { id: request.id, error: describe(error), fatal: true };
  }
  try {
    return { id: request.id, picture: await draw(request, ready) };
  } catch (error) {
    return { id: request.id, error: describe(error), fatal: false };
  }
}

// One model at a time, in the order asked.
let work = Promise.resolve();
addEventListener("message", (event: MessageEvent<PreviewRequest>) => {
  const request = event.data;
  work = work.then(async () => {
    postMessage(await answer(request));
  });
});
