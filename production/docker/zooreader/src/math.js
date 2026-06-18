/**
 * Clamp a value between min and max (inclusive).
 * @param {number} v
 * @param {number} min
 * @param {number} max
 * @returns {number}
 */
export const clamp = (v, min, max) => Math.max(min, Math.min(max, v));

/**
 * @param {{x: number, y: number, width: number, height: number}} a
 * @param {{x: number, y: number, width: number, height: number}} b
 * @returns {boolean}
 */
export function intersects(a, b) {
  return (
    a.x < b.x + b.width &&
    a.x + a.width > b.x &&
    a.y < b.y + b.height &&
    a.y + a.height > b.y
  );
}
