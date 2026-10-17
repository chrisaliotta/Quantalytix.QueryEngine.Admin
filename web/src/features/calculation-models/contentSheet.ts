/**
 * The title of a content row's sheet: `New variable` for a new row, `Edit BALANCE` for a draft's row, and `Variable
 * BALANCE` for a row of a version that is not a draft.
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

/** A text field's value as the API takes an optional text: null when it is blank. */
export function optionalText(value: string | null) {
  return value?.trim() ? value : null;
}
