import { ApiProblem } from '@/api/problemDetails';

/** What one pasted line reads as: a row of the list, or the reason it is none. */
export type LineReading<TRow> = { row: TRow } | { reason: string };

/** A pasted line that does not become a row. */
export interface UnresolvedLine {
  /** 1-based position in the pasted text. */
  lineNumber: number;
  /** The line as pasted. */
  text: string;
  reason: string;
}

/** The rows pasted text reads as, with the line each came from, and the lines that read as none. */
export interface PastedRows<TRow> {
  rows: TRow[];
  /** The line number of each row, by row index. */
  lineNumbers: number[];
  unresolved: UnresolvedLine[];
}

/**
 * Reads pasted text line by line. Blank lines are skipped; every other line is split at its commas, spaces around each
 * value are removed, and `readLine` turns the values into a row or the reason they are none.
 */
export function readPastedLines<TRow>(
  text: string,
  readLine: (values: string[]) => LineReading<TRow>
): PastedRows<TRow> {
  const pasted: PastedRows<TRow> = { rows: [], lineNumbers: [], unresolved: [] };
  text.split(/\r?\n/).forEach((line, index) => {
    if (line.trim() === '') {
      return;
    }
    const reading = readLine(line.split(',').map(value => value.trim()));
    if ('row' in reading) {
      pasted.rows.push(reading.row);
      pasted.lineNumbers.push(index + 1);
      return;
    }
    pasted.unresolved.push({ lineNumber: index + 1, text: line, reason: reading.reason });
  });
  return pasted;
}

/**
 * Puts a bulk replace's validation errors on one form field, each naming the pasted line of the item it concerns. The
 * API keys item errors by list index, such as `entries[3].sourceKeyValue`, which a paste form has no field for and its
 * user cannot match to a line.
 */
export function problemOnLines(error: unknown, list: string, lineNumbers: number[], field: string): unknown {
  if (!(error instanceof ApiProblem) || !error.errors) {
    return error;
  }
  const messages = Object.entries(error.errors).flatMap(([key, keyMessages]) => {
    const index = itemIndex(key, list);
    if (index === null) {
      return keyMessages;
    }
    return keyMessages.map(message => `Line ${lineNumbers[index]}: ${message}`);
  });
  return withErrors(error, { [field]: messages });
}

/**
 * Re-keys a bulk replace's validation errors for a form that edits the list item at `index`: that item's errors land
 * on the form's fields of the same names, and every other error keeps its key for the summary.
 */
export function problemOnItem(error: unknown, list: string, index: number): unknown {
  if (!(error instanceof ApiProblem) || !error.errors) {
    return error;
  }
  const errors = Object.fromEntries(
    Object.entries(error.errors).map(([key, messages]) => [itemField(key, `${list}[${index}].`), messages])
  );
  return withErrors(error, errors);
}

/** The list index an error key of the list's items names, such as 3 for `entries[3].dimensionMemberId`; null for other keys. */
function itemIndex(key: string, list: string): number | null {
  const match = /^(\w+)\[(\d+)\]/.exec(key);
  if (!match || match[1] !== list) {
    return null;
  }
  return Number(match[2]);
}

/** The form path of an error key: the field of the edited item when the key has the item's prefix, otherwise the key. */
function itemField(key: string, itemPrefix: string): string {
  if (key.startsWith(itemPrefix)) {
    return key.slice(itemPrefix.length);
  }
  return key;
}

/** The same problem with other validation errors. */
function withErrors(problem: ApiProblem, errors: Record<string, string[]>): ApiProblem {
  return new ApiProblem(problem.status, {
    title: problem.title,
    detail: problem.detail,
    errors,
    violations: problem.violations,
  });
}
