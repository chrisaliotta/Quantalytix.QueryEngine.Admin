/** Optional text as the API takes it: null when blank. */
export function emptyToNull(text: string): string | null {
  return text.trim() === '' ? null : text;
}

/** A count with its noun, such as `1 entry` or `3 entries`. */
export function count(total: number, noun: readonly [string, string]) {
  return `${total} ${total === 1 ? noun[0] : noun[1]}`;
}
