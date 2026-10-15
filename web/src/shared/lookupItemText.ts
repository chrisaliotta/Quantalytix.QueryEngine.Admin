import type { LookupItem } from './types';

/**
 * What a picker shows for a lookup item: its key and its label, each once. A label that is the key, as a release's is,
 * or that starts with the key, as a version's `v2 [2026-07-01 .. open)` does, is shown alone.
 */
export function lookupItemText(item: LookupItem): string {
  if (item.label === item.key || item.label.startsWith(`${item.key} `)) {
    return item.label;
  }
  return `${item.key} — ${item.label}`;
}
