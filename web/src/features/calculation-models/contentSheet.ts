/**
 * The title of a content row's sheet: `New variable` for a new row, `Edit BALANCE` for a row of an editable version, and
 * `Variable BALANCE` for a row of a version that can no longer change.
 */
export function contentSheetTitle(noun: string, key: string | null, readOnly: boolean) {
  if (key === null) {
    return `New ${noun}`;
  }
  if (readOnly) {
    return `${noun.charAt(0).toUpperCase()}${noun.slice(1)} ${key}`;
  }
  return `Edit ${key}`;
}
