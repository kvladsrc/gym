import { LAYOUT } from "./config.js";
import { clamp } from "./math.js";

/** Canvas presentation only; simulation and PDF lifecycle live elsewhere. */
export function createRenderer({
  gameCtx,
  sprites,
  gameInfo,
  player,
  heroSpeech,
}) {
  let terrain,
    terrainTheme,
    enemies,
    projectiles,
    impactEffects,
    animationFrame;
  /** Draw placeholder enemies. */
  function drawEnemies() {
    for (const enemy of enemies) {
      gameCtx.save();
      if (enemy.type === "flyer") {
        if (
          sprites.gnatFlyer.complete &&
          sprites.gnatFlyer.naturalHeight !== 0
        ) {
          const frameIndex =
            Math.floor(animationFrame / 5 + enemy.y / 18) %
            LAYOUT.FLYER_SPRITE_FRAMES;
          const frameWidth =
            sprites.gnatFlyer.naturalWidth / LAYOUT.FLYER_SPRITE_FRAMES;
          const flyingLeft =
            enemy.x + enemy.width / 2 > player.x + player.width / 2;

          if (flyingLeft) {
            gameCtx.translate(enemy.x + enemy.width, enemy.y);
            gameCtx.scale(-1, 1);
            gameCtx.drawImage(
              sprites.gnatFlyer,
              frameIndex * frameWidth,
              0,
              frameWidth,
              sprites.gnatFlyer.naturalHeight,
              0,
              0,
              enemy.width,
              enemy.height,
            );
          } else {
            gameCtx.drawImage(
              sprites.gnatFlyer,
              frameIndex * frameWidth,
              0,
              frameWidth,
              sprites.gnatFlyer.naturalHeight,
              enemy.x,
              enemy.y,
              enemy.width,
              enemy.height,
            );
          }
        } else {
          gameCtx.fillStyle = "#111";
          gameCtx.beginPath();
          gameCtx.arc(
            enemy.x + enemy.width / 2,
            enemy.y + enemy.height / 2,
            enemy.width / 2,
            0,
            Math.PI * 2,
          );
          gameCtx.fill();
          gameCtx.fillStyle = "#fff";
          gameCtx.fillRect(enemy.x + 9, enemy.y + 12, 5, 5);
          gameCtx.fillRect(enemy.x + enemy.width - 14, enemy.y + 12, 5, 5);
        }
      } else if (
        sprites.leechCrawler.complete &&
        sprites.leechCrawler.naturalHeight !== 0
      ) {
        const frameIndex =
          Math.floor(animationFrame / 8 + Math.max(0, enemy.x) / 24) %
          LAYOUT.CRAWLER_SPRITE_FRAMES;
        const frameWidth =
          sprites.leechCrawler.naturalWidth / LAYOUT.CRAWLER_SPRITE_FRAMES;

        if (enemy.vx < 0) {
          gameCtx.translate(enemy.x + enemy.width, enemy.y);
          gameCtx.scale(-1, 1);
          gameCtx.drawImage(
            sprites.leechCrawler,
            frameIndex * frameWidth,
            0,
            frameWidth,
            sprites.leechCrawler.naturalHeight,
            0,
            0,
            enemy.width,
            enemy.height,
          );
        } else {
          gameCtx.drawImage(
            sprites.leechCrawler,
            frameIndex * frameWidth,
            0,
            frameWidth,
            sprites.leechCrawler.naturalHeight,
            enemy.x,
            enemy.y,
            enemy.width,
            enemy.height,
          );
        }
      } else {
        gameCtx.fillStyle = "#111";
        gameCtx.fillRect(enemy.x, enemy.y, enemy.width, enemy.height);
        gameCtx.fillStyle = "#fff";
        gameCtx.fillRect(enemy.x + 8, enemy.y + 9, 5, 5);
        gameCtx.fillRect(enemy.x + enemy.width - 13, enemy.y + 9, 5, 5);
      }
      gameCtx.restore();
    }
  }

  /** Draw placeholder bullets. */
  function drawProjectiles() {
    for (const projectile of projectiles) {
      gameCtx.save();
      gameCtx.fillStyle = "#cc8d3d";
      if (projectile.type === "knife") {
        gameCtx.globalAlpha = 0.35;
        gameCtx.strokeStyle = "#243c3b";
        gameCtx.lineWidth = 3;
        gameCtx.beginPath();
        gameCtx.arc(
          projectile.x,
          projectile.y,
          LAYOUT.HERO_KNIFE_RANGE,
          0,
          Math.PI * 2,
        );
        gameCtx.stroke();
      } else if (projectile.type === "flamethrower") {
        gameCtx.globalAlpha = 0.45;
        gameCtx.beginPath();
        gameCtx.arc(projectile.x, projectile.y, 18, 0, Math.PI * 2);
        gameCtx.fill();
      } else if (projectile.type === "grenade") {
        if (sprites.grenade.complete && sprites.grenade.naturalHeight !== 0) {
          gameCtx.translate(projectile.x, projectile.y);
          gameCtx.rotate(projectile.age * 0.3);
          gameCtx.drawImage(sprites.grenade, -12, -12, 24, 24);
        } else {
          gameCtx.beginPath();
          gameCtx.arc(projectile.x, projectile.y, 7, 0, Math.PI * 2);
          gameCtx.fill();
        }
      } else {
        gameCtx.beginPath();
        gameCtx.arc(projectile.x, projectile.y, 4, 0, Math.PI * 2);
        gameCtx.fill();
      }
      gameCtx.restore();
    }
  }

  // ---------------------------------------------------------------------------
  // Game engine — draw (decomposed into sub-functions)
  // ---------------------------------------------------------------------------

  /** Draw the tiled terrain floor. */
  function drawTerrain() {
    const terrainSegments =
      terrain.length > 0
        ? terrain
        : [{ x: 0, width: gameInfo.width, level: 0 }];
    const terrainBlock = sprites.terrainBlocks?.[terrainTheme];
    const loadedTerrainBlock =
      terrainBlock?.complete && terrainBlock.naturalHeight !== 0
        ? terrainBlock
        : Object.values(sprites.terrainBlocks ?? {}).find(
            (block) => block.complete && block.naturalHeight !== 0,
          );

    if (!loadedTerrainBlock) return;

    for (let index = 0; index < terrainSegments.length; index++) {
      const segment = terrainSegments[index];
      if (segment.level === 2) {
        continue;
      }

      const surfaceY =
        gameInfo.groundY - segment.level * LAYOUT.TERRAIN_LEVEL_HEIGHT;
      const blockSize = LAYOUT.TERRAIN_SEGMENT_WIDTH;
      const drawY = surfaceY;
      const left = terrainSegments[index - 1];
      const right = terrainSegments[index + 1];
      const connectedLeft = left && left.level === segment.level;
      const connectedRight = right && right.level === segment.level;

      gameCtx.save();
      gameCtx.drawImage(
        loadedTerrainBlock,
        segment.x,
        drawY,
        blockSize,
        blockSize,
      );

      gameCtx.strokeStyle = "#243c3b";
      gameCtx.lineWidth = 2;
      gameCtx.beginPath();
      gameCtx.moveTo(segment.x, drawY);
      gameCtx.lineTo(segment.x + blockSize, drawY);
      if (!connectedLeft) {
        gameCtx.moveTo(segment.x, drawY);
        gameCtx.lineTo(segment.x, drawY + blockSize);
      }
      if (!connectedRight) {
        gameCtx.moveTo(segment.x + blockSize, drawY);
        gameCtx.lineTo(segment.x + blockSize, drawY + blockSize);
      }
      gameCtx.moveTo(segment.x, drawY + blockSize);
      gameCtx.lineTo(segment.x + blockSize, drawY + blockSize);
      gameCtx.stroke();
      gameCtx.restore();
    }
  }

  /** Draw a simple jetpack flame while the hero crosses gaps or level changes. */
  function drawJetpack(px, py, pw, ph) {
    if (!player.jetpackActive) return;

    gameCtx.save();
    gameCtx.fillStyle = "#cc8d3d";
    const flameX = px + pw / 2 - 5;
    const flameY = py + ph - 2;
    gameCtx.beginPath();
    gameCtx.moveTo(flameX, flameY);
    gameCtx.lineTo(flameX + 10, flameY);
    gameCtx.lineTo(
      flameX + 5,
      flameY + 14 + Math.sin(animationFrame * 0.6) * 4,
    );
    gameCtx.closePath();
    gameCtx.fill();
    if (player.turboJetpackActive) {
      gameCtx.fillRect(
        px + (player.facingRight ? -18 : pw + 6),
        py + ph * 0.35,
        24,
        8,
      );
    }
    gameCtx.restore();
  }

  /** Draw short-lived impact flashes for hits. */
  function drawImpactEffects() {
    for (const effect of impactEffects) {
      const progress = clamp(effect.age / effect.ttl, 0, 1);
      const radius = 8 + progress * 24;

      gameCtx.save();
      gameCtx.globalAlpha = 1 - progress;
      gameCtx.strokeStyle = "#243c3b";
      gameCtx.lineWidth = 3;
      gameCtx.beginPath();
      gameCtx.moveTo(effect.x, effect.y - radius);
      gameCtx.lineTo(effect.x + radius, effect.y);
      gameCtx.lineTo(effect.x, effect.y + radius);
      gameCtx.lineTo(effect.x - radius, effect.y);
      gameCtx.closePath();
      gameCtx.stroke();
      gameCtx.restore();
    }
  }

  /** Draw the hero's short automatic melee shockwave. */
  function drawAttackArc(px, py, pw, ph) {
    if (player.attackTimer <= 0) return;

    const progress = 1 - player.attackTimer / LAYOUT.ATTACK_TTL_FRAMES;
    const radius = LAYOUT.HERO_KNIFE_RANGE * (0.75 + progress * 0.25);

    gameCtx.save();
    gameCtx.globalAlpha = 1 - progress * 0.35;
    gameCtx.strokeStyle = "#243c3b";
    gameCtx.lineWidth = 4;
    gameCtx.beginPath();
    gameCtx.arc(px + pw / 2, py + ph / 2, radius, 0, Math.PI * 2);
    gameCtx.stroke();
    gameCtx.restore();
  }

  /** Draw the current LLM-generated hero line, if any. */
  function drawSpeechBubble() {
    const text = heroSpeech.currentText();
    if (!text) return;

    const maxWidth = 280;
    const lineHeight = 18;
    const words = text.split(/\s+/);
    const lines = [];
    let currentLine = "";

    gameCtx.save();
    gameCtx.font = "bold 14px 'Courier New', monospace";
    for (const word of words) {
      const candidate = currentLine ? `${currentLine} ${word}` : word;
      if (gameCtx.measureText(candidate).width > maxWidth && currentLine) {
        lines.push(currentLine);
        currentLine = word;
      } else {
        currentLine = candidate;
      }
    }
    if (currentLine) lines.push(currentLine);

    const bubbleWidth =
      Math.min(
        maxWidth,
        Math.max(...lines.map((line) => gameCtx.measureText(line).width)),
      ) + 24;
    const bubbleHeight = lines.length * lineHeight + 20;
    const x = clamp(
      player.x + player.width / 2 - bubbleWidth / 2,
      8,
      gameInfo.width - bubbleWidth - 8,
    );
    const y = clamp(player.y - bubbleHeight - 18, 8, gameInfo.gameHeight - 80);

    gameCtx.fillStyle = "#fff";
    gameCtx.strokeStyle = "#243c3b";
    gameCtx.lineWidth = 3;
    gameCtx.fillRect(x, y, bubbleWidth, bubbleHeight);
    gameCtx.strokeRect(x, y, bubbleWidth, bubbleHeight);

    gameCtx.beginPath();
    gameCtx.moveTo(x + bubbleWidth / 2 - 8, y + bubbleHeight);
    gameCtx.lineTo(x + bubbleWidth / 2 + 8, y + bubbleHeight);
    gameCtx.lineTo(player.x + player.width / 2, y + bubbleHeight + 12);
    gameCtx.closePath();
    gameCtx.fill();
    gameCtx.stroke();

    gameCtx.fillStyle = "#243c3b";
    lines.forEach((line, index) => {
      gameCtx.fillText(line, x + 12, y + 18 + index * lineHeight);
    });
    gameCtx.restore();
  }

  /** Draw the player sprite (with flip and squash-stretch). */
  function drawPlayer() {
    let pw = player.width;
    let ph = player.height;
    const bob = Math.sin(animationFrame * 0.2) * 2;

    if (!player.isGrounded) {
      ph += 2;
      pw -= 2;
    } else if (Math.abs(player.vx) > 3) {
      pw += 2;
      ph -= 2;
    }

    const px = Math.round(player.x + (player.width - pw) / 2);
    const py = Math.round(player.y + (player.height - ph) + 6 + bob);

    drawAttackArc(px, py, pw, ph);
    drawJetpack(px, py, pw, ph);

    gameCtx.save();
    gameCtx.imageSmoothingEnabled = false;
    if (player.facingRight) {
      gameCtx.translate(px + pw / 2, py);
      gameCtx.scale(-1, 1);
      gameCtx.translate(-(px + pw / 2), -py);
    }

    const moving = Math.abs(player.vx) > 0.4;
    const stateName =
      player.weaponTimer > 0
        ? player.weaponState
        : player.jetpackActive
          ? "jetpack"
          : !player.isGrounded
            ? "jump"
            : moving
              ? "run"
              : "idle";
    const playerSprite =
      sprites.playerStates[stateName] ?? sprites.playerStates.run;

    if (playerSprite.complete && playerSprite.naturalHeight !== 0) {
      const animated =
        moving ||
        player.jetpackActive ||
        player.weaponTimer > 0 ||
        !player.isGrounded;
      const isSheet = playerSprite.naturalWidth > playerSprite.naturalHeight;
      const frameCount = isSheet ? LAYOUT.PLAYER_SPRITE_FRAMES : 1;
      const frameIndex =
        frameCount > 1 && animated
          ? Math.floor(animationFrame / 7) % frameCount
          : 0;
      const frameWidth = playerSprite.naturalWidth / frameCount;
      gameCtx.drawImage(
        playerSprite,
        frameIndex * frameWidth,
        0,
        frameWidth,
        playerSprite.naturalHeight,
        px,
        py,
        pw,
        ph,
      );
    } else {
      gameCtx.fillStyle = "#cc8d3d";
      gameCtx.fillRect(px, py, pw, ph);
    }

    gameCtx.restore();
  }

  return (scene) => {
    ({
      terrain,
      terrainTheme,
      enemies,
      projectiles,
      impactEffects,
      animationFrame,
    } = scene);
    drawTerrain();
    drawEnemies();
    drawProjectiles();
    drawImpactEffects();
    drawPlayer();
    drawSpeechBubble();
  };
}
