/** Optional text as the API takes it: null when blank. */
export function emptyToNull(text: string): string | null {
  return text.trim() === '' ? null : text;
}
