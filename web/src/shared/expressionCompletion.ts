/** A completion offered while typing an expression reference such as `[f.amount]`. */
export interface ExpressionCompletion {
  /** Text shown in the suggestion list. */
  label: string;
  /** Text inserted in place of the last `replaceLength` characters before the cursor. */
  insertText: string;
  replaceLength: number;
  /** `reference` is the `[f.` / `[m.` prefix itself; picking it should open the key list. */
  kind: 'reference' | 'field' | 'measure';
}

/** `[f.` or `[m.` followed by a partial key, at the end of the text. */
const KEY_PATTERN = /\[([fm])\.([^\]\s]*)$/;

/** An opening bracket, optionally followed by the namespace letter, at the end of the text. */
const REFERENCE_PATTERN = /\[[fm]?$/;

/**
 * Completions for the reference being typed at the end of `textBeforeCursor`: after `[` it offers
 * `[f.` and `[m.`; after `[f.` the field keys and after `[m.` the measure keys, each inserted with
 * the closing `]`. Returns nothing outside a reference. The editor filters by what is typed.
 */
export function getExpressionCompletions(
  textBeforeCursor: string,
  fieldKeys: string[],
  measureKeys: string[]
): ExpressionCompletion[] {
  const key = KEY_PATTERN.exec(textBeforeCursor);
  if (key) {
    const [, namespace, partialKey] = key;
    const kind = namespace === 'f' ? 'field' : 'measure';
    const keys = namespace === 'f' ? fieldKeys : measureKeys;
    return keys.map(candidate => ({
      label: candidate,
      insertText: `${candidate}]`,
      replaceLength: partialKey.length,
      kind,
    }));
  }

  const reference = REFERENCE_PATTERN.exec(textBeforeCursor);
  if (reference) {
    const replaceLength = reference[0].length;
    return [
      { label: '[f.', insertText: '[f.', replaceLength, kind: 'reference' },
      { label: '[m.', insertText: '[m.', replaceLength, kind: 'reference' },
    ];
  }

  return [];
}
