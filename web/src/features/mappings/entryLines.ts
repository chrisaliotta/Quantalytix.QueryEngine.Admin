import { readPastedLines, type PastedRows } from '@/shared/bulkLists';
import type { LookupItem } from '@/shared/types';
import type { MappingEntry, MappingEntryInput } from './types';

/** The line format of pasted entries. */
export const ENTRY_LINE_FORMAT = 'source_key,member_key';

/**
 * Reads pasted `source_key,member_key` lines into entries, resolving each member key among the members of the pinned
 * dimension version. Source keys keep their leading zeros, so `003000` and `3000` stay different keys.
 */
export function readEntryLines(text: string, members: LookupItem[]): PastedRows<MappingEntryInput> {
  const memberIds = new Map(members.map(member => [member.key, member.id]));
  return readPastedLines<MappingEntryInput>(text, values => {
    if (values.length !== 2) {
      return { reason: `Expected ${ENTRY_LINE_FORMAT}.` };
    }
    const [sourceKeyValue, memberKey] = values;
    const memberId = memberIds.get(memberKey);
    if (memberId === undefined) {
      return { reason: `No member of the pinned version has the key '${memberKey}'.` };
    }
    return { row: { sourceKeyValue, memberId } };
  });
}

/** Writes entries as `source_key,member_key` lines; an entry whose member is not among the members is left out. */
export function writeEntryLines(entries: MappingEntry[], members: LookupItem[]): string {
  const memberKeys = new Map(members.map(member => [member.id, member.key]));
  return entries
    .flatMap(entry => {
      const memberKey = memberKeys.get(entry.memberId);
      if (memberKey === undefined) {
        return [];
      }
      return [`${entry.sourceKeyValue},${memberKey}`];
    })
    .join('\n');
}
