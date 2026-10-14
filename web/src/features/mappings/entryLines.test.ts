/**
 * Why these tests exist: the entries bulk paste resolves member keys to the member IDs the API stores, so a key must
 * resolve among the pinned version's members only, and source keys must arrive exactly as typed, because the engine
 * treats `003000` and `3000` as different values. The dialog starts with the current entries written as lines, which
 * must read back as the same entries so that a replace keeps what the user leaves alone.
 */
import { describe, expect, it } from 'vitest';
import type { LookupItem } from '@/shared/types';
import { readEntryLines, writeEntryLines } from './entryLines';

const members: LookupItem[] = [
  { id: 201, key: 'CA', label: 'California', parentId: 200 },
  { id: 202, key: 'NY', label: 'New York', parentId: 200 },
];

describe('readEntryLines', () => {
  it('resolves member keys and keeps leading zeros', () => {
    const pasted = readEntryLines('003000,CA\n3000,NY', members);

    expect(pasted.rows).toEqual([
      { sourceKeyValue: '003000', memberId: 201 },
      { sourceKeyValue: '3000', memberId: 202 },
    ]);
    expect(pasted.unresolved).toEqual([]);
  });

  it('lists the lines with an unknown member key or another number of values', () => {
    const pasted = readEntryLines('06,CA\n48,TX\n36;NY\n32,NV,extra', members);

    expect(pasted.rows).toEqual([{ sourceKeyValue: '06', memberId: 201 }]);
    expect(pasted.unresolved.map(line => [line.lineNumber, line.reason])).toEqual([
      [2, "No member of the pinned version has the key 'TX'."],
      [3, 'Expected source_key,member_key.'],
      [4, 'Expected source_key,member_key.'],
    ]);
  });
});

describe('writeEntryLines', () => {
  it('writes the entries as lines that read back as the same entries', () => {
    const entries = [
      { mappingEntryId: 1, mappingVersionId: 40, sourceKeyValue: '003000', memberId: 201 },
      { mappingEntryId: 2, mappingVersionId: 40, sourceKeyValue: '36', memberId: 202 },
    ];

    const text = writeEntryLines(entries, members);

    expect(text).toBe('003000,CA\n36,NY');
    expect(readEntryLines(text, members).rows).toEqual([
      { sourceKeyValue: '003000', memberId: 201 },
      { sourceKeyValue: '36', memberId: 202 },
    ]);
  });
});
