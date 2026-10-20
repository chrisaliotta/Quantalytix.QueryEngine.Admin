import { readPastedLines, type PastedRows } from '@/shared/bulkLists';
import type { MappingEntry, MappingEntryInput } from './types';

/** The line format of pasted entries. */
export const ENTRY_LINE_FORMAT = 'source_key,member_key';

/**
 * Reads pasted `source_key,member_key` lines into entries. Both keys are kept exactly as typed: source keys keep their
 * leading zeros, so `003000` and `3000` stay different keys, and member keys resolve in the dimension's published version
 * when requests read them, which the publication rules check.
 */
export function readEntryLines(text: string): PastedRows<MappingEntryInput> {
  return readPastedLines<MappingEntryInput>(text, values => {
    if (values.length !== 2) {
      return { reason: `Expected ${ENTRY_LINE_FORMAT}.` };
    }
    const [sourceKeyValue, memberKey] = values;
    return { row: { sourceKeyValue, memberKey } };
  });
}

/** Writes entries as `source_key,member_key` lines. */
export function writeEntryLines(entries: MappingEntry[]): string {
  return entries.map(entry => `${entry.sourceKeyValue},${entry.memberKey}`).join('\n');
}
