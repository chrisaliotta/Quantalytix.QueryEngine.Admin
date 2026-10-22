import type { LookupItem } from './types';

/** Picker items for keys that are their own labels, such as a text parameter's allowed values. */
export function keyItems(keys: readonly string[]): LookupItem[] {
  return keys.map((key, index) => ({ id: index, key, label: key }));
}
