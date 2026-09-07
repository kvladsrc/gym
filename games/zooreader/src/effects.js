import { LAYOUT } from "./config.js";

/**
 * Add a short visual effect where an obstacle was cleared.
 * @param {{x: number, y: number, width: number, height: number}} ob
 * @param {{x: number, y: number, age: number, ttl: number}[]} effects
 * @returns {{x: number, y: number, age: number, ttl: number}[]}
 */
export function addImpactEffect(ob, effects) {
  return [
    ...effects,
    {
      x: ob.x + ob.width / 2,
      y: ob.y + ob.height / 2,
      age: 0,
      ttl: LAYOUT.IMPACT_TTL_FRAMES,
    },
  ];
}

/**
 * @param {{age: number}[]} effects
 * @param {number} dt
 * @returns {{age: number}[]}
 */
export function updateEffectAges(effects, dt) {
  return effects.map((effect) => ({ ...effect, age: effect.age + dt }));
}

/**
 * @param {{age: number, ttl: number}[]} effects
 * @returns {{age: number, ttl: number}[]}
 */
export function clearExpiredEffects(effects) {
  return effects.filter((effect) => effect.age < effect.ttl);
}
