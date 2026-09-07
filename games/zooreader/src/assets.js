/**
 * @param {string} src
 * @returns {HTMLImageElement}
 */
function loadImage(src) {
  const img = new Image();
  img.src = src;
  return img;
}

/**
 * @returns {{
 *   playerStates: Record<string, HTMLImageElement>,
 *   grenade: HTMLImageElement,
 *   leechCrawler: HTMLImageElement,
 *   gnatFlyer: HTMLImageElement,
 *   terrainBlocks: Record<string, HTMLImageElement>,
 * }}
 */
export function loadSprites() {
  return {
    playerStates: {
      idle: loadImage("assets/player.png"),
      run: loadImage("assets/player-run.png"),
      jump: loadImage("assets/player-jump.png"),
      jetpack: loadImage("assets/player-jetpack.png"),
      knife: loadImage("assets/player-knife.png"),
      grenade: loadImage("assets/player-grenade.png"),
      flamethrower: loadImage("assets/player-flamethrower.png"),
      pistol: loadImage("assets/player-pistol.png"),
    },
    grenade: loadImage("assets/grenade.png"),
    leechCrawler: loadImage("assets/leech-crawler.png"),
    gnatFlyer: loadImage("assets/gnat-flyer.png"),
    terrainBlocks: {
      desert: loadImage("assets/block-desert.png"),
      grass: loadImage("assets/block-grass.png"),
      snow: loadImage("assets/block-snow.png"),
    },
  };
}
