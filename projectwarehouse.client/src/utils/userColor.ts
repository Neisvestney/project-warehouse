const NEUTRAL_COLOR = "grey.500";

export function userColor(userId: string | null | undefined): string {
  if (!userId) return NEUTRAL_COLOR;
  return `hsl(${hashToHue(userId)} 55% 45%)`;
}

// FNV-1a — стабильный между сессиями, в отличие от любого хэша на Math.random/порядке
function hashToHue(value: string): number {
  let hash = 0x811c9dc5;
  for (let i = 0; i < value.length; i++) {
    hash ^= value.charCodeAt(i);
    hash = Math.imul(hash, 0x01000193);
  }
  return Math.abs(hash) % 360;
}
