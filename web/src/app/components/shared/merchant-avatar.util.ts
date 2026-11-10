// Deterministic avatar styling for merchants so the same merchant always gets the same badge.

const AVATAR_COLOURS = [
  '#1d4e9e',
  '#0f766e',
  '#7c3aed',
  '#b45309',
  '#be185d',
  '#0369a1',
  '#4d7c0f',
  '#c2410c',
];

export function merchantInitials(merchant: string): string {
  const words = merchant
    .replace(/\.com$/i, '')
    .split(/[\s-]+/)
    .filter((w) => /[a-z0-9]/i.test(w));
  const letters = words.length > 1 ? words[0][0] + words[1][0] : (words[0] ?? '?').slice(0, 2);
  return letters.toUpperCase();
}

export function merchantColour(merchant: string): string {
  let hash = 0;
  for (const char of merchant.toLowerCase()) {
    hash = (hash * 31 + char.charCodeAt(0)) | 0;
  }
  return AVATAR_COLOURS[Math.abs(hash) % AVATAR_COLOURS.length];
}
