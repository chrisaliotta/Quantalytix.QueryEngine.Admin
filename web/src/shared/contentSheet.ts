/** The title of a content row's sheet: `New variable` for a new row, and `Edit BALANCE` for an existing one. */
export function contentSheetTitle(noun: string, key: string | null) {
  return key === null ? `New ${noun}` : `Edit ${key}`;
}
