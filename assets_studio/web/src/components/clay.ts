// Shared by the viewer and the thumbnail renderer's worker: no components here.

/** A shape without colour (Hunyuan3D: meta.color "none") comes with glTF's
 * default material, white and fully metallic: it renders as a flat white
 * silhouette. It is shown as grey matte clay instead, so the shape reads in
 * light and shade. Colour in linear RGB, as glTF's baseColorFactor. */
export const CLAY = {
  color: [0.62, 0.6, 0.57],
  metallic: 0,
  roughness: 0.85,
} as const;

export const isShapeOnly = (asset: { meta: Record<string, unknown> }) =>
  asset.meta.color === "none";
