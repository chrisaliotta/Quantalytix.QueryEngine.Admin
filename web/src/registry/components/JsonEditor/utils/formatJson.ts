function tryParseJson(value: string): { data: unknown; error: string | null } {
  const trimmed = value.trim();
  if (!trimmed) {
    return { data: null, error: null };
  }
  try {
    const data = JSON.parse(trimmed);
    return { data, error: null };
  } catch (e) {
    const message = e instanceof Error ? e.message : 'Invalid JSON';
    return { data: null, error: message };
  }
}

export function formatJson(value: string): string {
  const { data, error } = tryParseJson(value);
  if (error) return value;
  if (data === null && value.trim() === '') return value;
  return JSON.stringify(data, null, 2);
}
