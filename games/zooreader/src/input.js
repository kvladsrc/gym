export const keys = {
  ArrowLeft: false,
  ArrowRight: false,
  KeyA: false,
  KeyD: false,
};

export const isMovingLeft = () => keys.ArrowLeft || keys.KeyA;
export const isMovingRight = () => keys.ArrowRight || keys.KeyD;
export const isMoving = () => isMovingLeft() || isMovingRight();

export function clearKeys() {
  for (const code of Object.keys(keys)) keys[code] = false;
}

export function isEditing(target) {
  return Boolean(
    target?.isContentEditable ||
    target?.closest?.("input, textarea, select, [contenteditable]"),
  );
}
