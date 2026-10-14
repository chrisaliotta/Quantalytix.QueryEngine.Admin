export function isDarkBackground(background: string): boolean {
  const value = background.trim();

  const hexMatch = value.match(/^#([0-9a-f]{3}|[0-9a-f]{6})$/i);
  if (hexMatch) {
    let hex = hexMatch[1];
    if (hex.length === 3) {
      hex = hex
        .split('')
        .map(char => char + char)
        .join('');
    }

    const r = parseInt(hex.slice(0, 2), 16);
    const g = parseInt(hex.slice(2, 4), 16);
    const b = parseInt(hex.slice(4, 6), 16);
    const luminance = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;
    return luminance < 0.5;
  }

  if (value.startsWith('oklch(')) {
    const inside = value.slice('oklch('.length, value.indexOf(')'));
    const [l] = inside.split(/[ ,]+/).filter(Boolean);
    const lightness = parseFloat(l);
    if (!Number.isNaN(lightness)) {
      return lightness < 0.6;
    }
  }

  if (value.startsWith('rgb')) {
    const numbers = value
      .replace(/rgba?\(/, '')
      .replace(')', '')
      .split(',')
      .map(part => parseFloat(part.trim()));

    if (numbers.length >= 3) {
      const [r, g, b] = numbers;
      const luminance = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;
      return luminance < 0.5;
    }
  }

  return false;
}
