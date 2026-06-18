/**
 * @param {string} src
 * @returns {HTMLImageElement}
 */
function loadImage(src) {
  const img = new Image();
  img.src = src;
  return img;
}

/** @returns {{player: HTMLImageElement}} */
export function loadSprites() {
  return {
    player: loadImage("player.png"),
  };
}
