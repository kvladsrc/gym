import { LAYOUT } from "./config.js";

/**
 * @param {{x: number, y: number, width: number, height: number}} player
 * @param {{type: string, x: number, y: number, width: number, height: number, hp: number}[]} enemies
 * @returns {{enemy: {type: string, x: number, y: number, width: number, height: number, hp: number}, distance: number, aimX: number, aimY: number} | null}
 */
function findKnifeTarget(player, enemies) {
  const playerCenter = player.x + player.width / 2;
  const playerMiddle = player.y + player.height / 2;

  let bestTarget = null;
  let bestDistance = Infinity;
  let bestAimX = player.facingRight ? 1 : -1;
  let bestAimY = 0;
  for (const enemy of enemies) {
    if (enemy.hp <= 0) continue;

    const enemyCenter = enemy.x + enemy.width / 2;
    const enemyMiddle = enemy.y + enemy.height / 2;
    const dx = enemyCenter - playerCenter;
    const dy = enemyMiddle - playerMiddle;
    const distance = Math.max(1, Math.hypot(dx, dy));
    if (distance > LAYOUT.HERO_KNIFE_RANGE) continue;
    if (distance < bestDistance) {
      bestDistance = distance;
      bestTarget = enemy;
      bestAimX = dx / distance;
      bestAimY = dy / distance;
    }
  }

  return bestTarget
    ? {
        enemy: bestTarget,
        distance: bestDistance,
        aimX: bestAimX,
        aimY: bestAimY,
      }
    : null;
}

/**
 * @param {{x: number, y: number, width: number, height: number, facingRight: boolean}} player
 * @param {{type: string, x: number, y: number, width: number, height: number, hp: number}[]} enemies
 * @returns {{enemy: {type: string, x: number, y: number, width: number, height: number, hp: number}, distance: number, aimX: number, aimY: number} | null}
 */
function findForwardTarget(player, enemies) {
  const playerCenter = player.x + player.width / 2;
  const playerMiddle = player.y + player.height / 2;
  const direction = player.facingRight ? 1 : -1;

  let bestTarget = null;
  let bestDistance = Infinity;
  let bestAimX = direction;
  let bestAimY = 0;
  for (const enemy of enemies) {
    if (enemy.hp <= 0) continue;

    const enemyCenter = enemy.x + enemy.width / 2;
    const enemyMiddle = enemy.y + enemy.height / 2;
    const dx = enemyCenter - playerCenter;
    const dy = enemyMiddle - playerMiddle;
    const signedDistance = dx * direction;
    const distance = Math.max(1, Math.hypot(dx, dy));
    const cosAngle = signedDistance / distance;
    const insideAimCone =
      cosAngle >= Math.cos(LAYOUT.HERO_AIM_CONE_RADIANS / 2);
    if (signedDistance < 0 || distance > LAYOUT.HERO_TARGET_RANGE) {
      continue;
    }
    if (!insideAimCone) continue;
    if (distance < bestDistance) {
      bestDistance = distance;
      bestTarget = enemy;
      bestAimX = dx / distance;
      bestAimY = dy / distance;
    }
  }

  return bestTarget
    ? {
        enemy: bestTarget,
        distance: bestDistance,
        aimX: bestAimX,
        aimY: bestAimY,
      }
    : null;
}

/**
 * @param {{type: string}} target
 * @param {number} distance
 * @returns {"knife" | "flamethrower" | "grenade" | "rifle"}
 */
function chooseWeapon(target, distance) {
  if (distance <= LAYOUT.HERO_KNIFE_RANGE) return "knife";
  if (target.type === "crawler") {
    if (distance <= LAYOUT.HERO_FLAMETHROWER_RANGE) return "flamethrower";
    return "grenade";
  }
  return "rifle";
}

/**
 * Fire a placeholder weapon when the hero has a valid target.
 * @param {{x: number, y: number, width: number, height: number, facingRight: boolean, fireCooldown: number, attackTimer: number}} player
 * @param {{type: string, x: number, y: number, width: number, height: number, hp: number}[]} enemies
 * @param {{type: string, x: number, y: number, vx: number, vy: number, age: number, ttl: number}[]} projectiles
 * @returns {{type: string, x: number, y: number, vx: number, vy: number, age: number, ttl: number}[]}
 */
export function maybeFireAtTarget(player, enemies, projectiles) {
  const knifeTarget = findKnifeTarget(player, enemies);
  const target = knifeTarget || findForwardTarget(player, enemies);
  if (player.fireCooldown > 0 || !target) {
    return projectiles;
  }

  player.fireCooldown = LAYOUT.HERO_FIRE_COOLDOWN_FRAMES;
  const weapon = knifeTarget
    ? "knife"
    : chooseWeapon(target.enemy, target.distance);
  if (weapon === "knife") {
    player.attackTimer = LAYOUT.ATTACK_TTL_FRAMES;
    return [
      ...projectiles,
      {
        type: weapon,
        x: player.x + player.width / 2,
        y: player.y + player.height / 2,
        vx: 0,
        vy: 0,
        age: 0,
        ttl: 6,
      },
    ];
  }
  const direction = player.facingRight ? 1 : -1;
  const originX = player.x + player.width / 2 + direction * 18;
  const originY = player.y + player.height * 0.42;
  const aimX = target.aimX ?? direction;
  const aimY = target.aimY ?? 0;

  if (weapon === "flamethrower") {
    return [
      ...projectiles,
      {
        type: weapon,
        x: originX,
        y: originY,
        vx: aimX * 5,
        vy: aimY * 5,
        age: 0,
        ttl: 14,
      },
    ];
  }

  if (weapon === "grenade") {
    return [
      ...projectiles,
      {
        type: weapon,
        x: originX,
        y: originY,
        vx: aimX * LAYOUT.GRENADE_SPEED_X,
        vy: LAYOUT.GRENADE_SPEED_Y + aimY * 2,
        age: 0,
        ttl: LAYOUT.PROJECTILE_TTL_FRAMES,
      },
    ];
  }

  return [
    ...projectiles,
    {
      type: weapon,
      x: originX,
      y: originY,
      vx: aimX * LAYOUT.PROJECTILE_SPEED,
      vy: aimY * LAYOUT.PROJECTILE_SPEED,
      age: 0,
      ttl: LAYOUT.PROJECTILE_TTL_FRAMES,
    },
  ];
}

/**
 * @param {{type: string, x: number, y: number, vx: number, vy: number, age: number, ttl: number}[]} projectiles
 * @param {number} dt
 * @returns {{type: string, x: number, y: number, vx: number, vy: number, age: number, ttl: number}[]}
 */
export function updateProjectiles(projectiles, dt) {
  return projectiles
    .map((projectile) => {
      const gravity =
        projectile.type === "grenade" ? LAYOUT.GRENADE_GRAVITY * dt : 0;
      return {
        ...projectile,
        x: projectile.x + projectile.vx * dt,
        y: projectile.y + projectile.vy * dt,
        vy: projectile.vy + gravity,
        age: projectile.age + dt,
      };
    })
    .filter((projectile) => projectile.age < projectile.ttl);
}
