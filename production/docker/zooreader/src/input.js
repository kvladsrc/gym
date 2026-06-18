export const keys = {
  ArrowLeft: false,
  ArrowRight: false,
  KeyA: false,
  KeyD: false,
};

export const isMovingLeft = () => keys.ArrowLeft || keys.KeyA;
export const isMovingRight = () => keys.ArrowRight || keys.KeyD;
export const isMoving = () => isMovingLeft() || isMovingRight();
