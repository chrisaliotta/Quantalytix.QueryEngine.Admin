let colorCanvas: HTMLCanvasElement | null = null;
let colorCtx: CanvasRenderingContext2D | null = null;

export function normalizeColorForMonaco(color: string): string {
  const value = color.trim();
  if (!value || typeof window === 'undefined') return value;

  try {
    if (!colorCanvas) {
      colorCanvas = window.document.createElement('canvas');
      colorCanvas.width = colorCanvas.height = 1;
      colorCtx = colorCanvas.getContext('2d');
    }

    if (!colorCtx) return value;

    colorCtx.clearRect(0, 0, 1, 1);
    colorCtx.fillStyle = value;
    colorCtx.fillRect(0, 0, 1, 1);
    const data = colorCtx.getImageData(0, 0, 1, 1).data;
    const [r, g, b] = data;

    const toHex = (channel: number) => channel.toString(16).padStart(2, '0');
    return `#${toHex(r)}${toHex(g)}${toHex(b)}`;
  } catch {
    return value;
  }
}
