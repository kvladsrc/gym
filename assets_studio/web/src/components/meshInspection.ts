// model-viewer 4.3.1 has no public wireframe API. Keep its scene access here;
// the browser walkthrough checks this adapter when upgrading model-viewer.
import { $scene } from "@google/model-viewer/lib/model-viewer-base.js";
import type { ModelScene } from "@google/model-viewer/lib/three-components/ModelScene.js";
import {
  Color,
  DoubleSide,
  InstancedMesh,
  Mesh,
  MeshBasicMaterial,
  MeshStandardMaterial,
  PlaneGeometry,
  ShaderMaterial,
  Vector2,
  Vector3,
} from "three";
import type { Material } from "three";

export type MeshView = "material" | "geometry" | "wireframe";

function sceneOf(element: HTMLElement) {
  return (element as HTMLElement & { [$scene]: ModelScene })[$scene];
}

export function inspectMesh(element: HTMLElement) {
  const scene = sceneOf(element);
  const meshes: Mesh[] = [];
  scene.model.traverse((node) => {
    if (node instanceof Mesh) {
      const mesh = node as Mesh;
      if (!scene.bakedShadows.has(mesh)) meshes.push(mesh);
    }
  });
  const stats = meshes.reduce(
    (total, mesh) => {
      const vertices = mesh.geometry.getAttribute("position").count;
      const instances = mesh instanceof InstancedMesh ? mesh.count : 1;
      total.vertices += vertices * instances;
      total.triangles +=
        Math.floor((mesh.geometry.index?.count ?? vertices) / 3) * instances;
      return total;
    },
    { vertices: 0, triangles: 0 },
  );
  const wire = new MeshBasicMaterial({
    color: 0x10252e,
    wireframe: true,
    depthWrite: false,
    toneMapped: false,
  });
  const entries = meshes.map((mesh) => {
    const original = mesh.material;
    const sources = Array.isArray(original) ? original : [original];
    const clay = sources.map(
      (material) =>
        new MeshStandardMaterial({
          color: 0xa9a5a0,
          metalness: 0,
          roughness: 0.85,
          side: material.side,
        }),
    );
    // Offset the surface rather than the lines, so hidden back edges stay hidden.
    const textured = sources.map((material) => {
      const copy = material.clone();
      copy.polygonOffset = true;
      copy.polygonOffsetFactor = 1;
      copy.polygonOffsetUnits = 1;
      return copy;
    });
    let overlay: Mesh | null = null;
    function materialSet(materials: Material[]) {
      return Array.isArray(original) ? materials : (materials[0] ?? original);
    }
    return {
      setView(mode: MeshView, texture: boolean) {
        for (const material of clay) {
          material.polygonOffset = mode === "wireframe";
          material.polygonOffsetFactor = 1;
          material.polygonOffsetUnits = 1;
        }
        mesh.material =
          mode === "material"
            ? original
            : materialSet(mode === "wireframe" && texture ? textured : clay);
        if (mode === "wireframe" && !overlay) {
          // Clone only the mesh node: share geometry, skeleton and morph state.
          // Build lazily so ordinary previews incur no wireframe GPU allocation.
          overlay = mesh.clone(false);
          overlay.material = wire;
          overlay.position.set(0, 0, 0);
          overlay.quaternion.identity();
          overlay.scale.set(1, 1, 1);
          overlay.matrix.identity();
          overlay.matrixAutoUpdate = true;
          overlay.morphTargetInfluences = mesh.morphTargetInfluences;
          overlay.renderOrder = mesh.renderOrder + 1;
          overlay.raycast = () => {}; // Preserve model-viewer's surface picking.
          mesh.add(overlay);
        }
        if (overlay) overlay.visible = mode === "wireframe";
      },
      dispose() {
        overlay?.removeFromParent();
        mesh.material = original;
        for (const material of [...clay, ...textured]) material.dispose();
      },
    };
  });
  return {
    stats,
    setView(mode: MeshView, texture: boolean) {
      for (const entry of entries) entry.setView(mode, texture);
      scene.queueRender();
    },
    dispose() {
      for (const entry of entries) entry.dispose();
      wire.dispose();
      scene.queueRender();
    },
  };
}

/** Grid cells as powers of ten in the file's units (glTF: metres): `major`
 * is the largest power of ten not above the model's largest side, `minor` a
 * tenth of it, so a model always spans 1–10 large cells. */
export function gridCells(extent: number) {
  const exponent =
    extent > 0 && Number.isFinite(extent)
      ? Math.floor(Math.log10(extent) + 1e-9)
      : 0;
  return { exponent, major: 10 ** exponent, minor: 10 ** (exponent - 1) };
}

const GRID_VERTEX = /* glsl */ `
uniform vec2 uOffset;
varying vec2 vXZ;
void main() {
  vXZ = position.xz + uOffset;
  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
}`;

// Lines a pixel or so wide at any distance (screen-space derivatives); minor
// lines fade out before they crowd into a grey wash, the edge fades to nothing.
const GRID_FRAGMENT = /* glsl */ `
uniform vec3 uColor;
uniform vec2 uCenter;
uniform float uMinor;
uniform float uMajor;
uniform float uRadius;
varying vec2 vXZ;
float lines(float cell, float width) {
  vec2 coord = vXZ / cell;
  vec2 change = fwidth(coord);
  vec2 distance = abs(fract(coord - 0.5) - 0.5) / (change * width);
  float line = 1.0 - min(min(distance.x, distance.y), 1.0);
  return line * (1.0 - smoothstep(0.12, 0.35, max(change.x, change.y)));
}
void main() {
  float alpha = max(lines(uMinor, 1.0) * 0.14, lines(uMajor, 1.0) * 0.32);
  alpha *= 1.0 - smoothstep(0.45, 1.0, length(vXZ - uCenter) / uRadius);
  if (alpha < 0.004) discard;
  gl_FragColor = vec4(uColor, alpha);
  #include <colorspace_fragment>
}`;

/** A grid on the floor under the model (its lowest point), aligned to the
 * file's origin. Not part of the model: framing, counts and picking ignore it. */
export function addFloorGrid(element: HTMLElement) {
  const scene = sceneOf(element);
  const box = scene.boundingBox;
  const size = box.getSize(new Vector3());
  const center = box.getCenter(new Vector3());
  const extent = Math.max(size.x, size.y, size.z);
  const cells = gridCells(extent);
  const radius = Math.ceil((1.5 * extent) / cells.major + 1) * cells.major;
  const geometry = new PlaneGeometry(radius * 2, radius * 2).rotateX(
    -Math.PI / 2,
  );
  const lineColor = new Color();
  const material = new ShaderMaterial({
    uniforms: {
      uColor: { value: lineColor },
      uOffset: { value: new Vector2(center.x, center.z) },
      uCenter: { value: new Vector2(center.x, center.z) },
      uMinor: { value: cells.minor },
      uMajor: { value: cells.major },
      uRadius: { value: radius },
    },
    vertexShader: GRID_VERTEX,
    fragmentShader: GRID_FRAGMENT,
    transparent: true,
    depthWrite: false,
    side: DoubleSide,
    // A flat model lying on the floor stays in front of the lines.
    polygonOffset: true,
    polygonOffsetFactor: 1,
    polygonOffsetUnits: 1,
  });
  const grid = new Mesh(geometry, material);
  grid.name = "floor-grid";
  grid.position.set(center.x, box.min.y, center.z);
  grid.renderOrder = -1;
  grid.raycast = () => {}; // Taps and hotspots hit the model, not the floor.
  scene.target.add(grid);
  return {
    cells,
    /** `color`: a CSS colour, the theme's text colour. */
    set(visible: boolean, color: string) {
      grid.visible = visible;
      lineColor.setStyle(color);
      scene.queueRender();
    },
    dispose() {
      grid.removeFromParent();
      geometry.dispose();
      material.dispose();
      scene.queueRender();
    },
  };
}
