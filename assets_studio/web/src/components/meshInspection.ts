// model-viewer 4.3.1 has no public wireframe API. Keep its scene access here;
// the browser walkthrough checks this adapter when upgrading model-viewer.
import { $scene } from "@google/model-viewer/lib/model-viewer-base.js";
import type { ModelScene } from "@google/model-viewer/lib/three-components/ModelScene.js";
import {
  InstancedMesh,
  Mesh,
  MeshBasicMaterial,
  MeshStandardMaterial,
} from "three";
import type { Material } from "three";

export type MeshView = "material" | "geometry" | "wireframe";

export function inspectMesh(element: HTMLElement) {
  const scene = (element as HTMLElement & { [$scene]: ModelScene })[$scene];
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
