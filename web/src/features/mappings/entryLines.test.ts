/**
 * Why these tests exist: the entries bulk paste replaces every entry of a version, and the engine compares source keys
 * exactly, treating `003000` and `3000` as different values, so pasted keys must arrive exactly as typed. Member keys are
 * stored as keys and resolve in the dimension's published version, so a line is refused only for its shape. The dialog
 * starts with the current entries written as lines, which must read back as the same entries so that a replace keeps
 * what the user leaves alone.
 */
import { describe, expect, it } from 'vitest';
import { readEntryLines, writeEntryLines } from './entryLines';

describe('readEntryLines', () => {
  it('keeps source keys and member keys exactly as typed', () => {
    const pasted = readEntryLines('003000,CA\n3000,NY');

    expect(pasted.rows).toEqual([
      { sourceKeyValue: '003000', memberKey: 'CA' },
      { sourceKeyValue: '3000', memberKey: 'NY' },
    ]);
    expect(pasted.unresolved).toEqual([]);
  });

  it('lists the lines with another number of values', () => {
    const pasted = readEntryLines('06,CA\n36;NY\n32,NV,extra');

    expect(pasted.rows).toEqual([{ sourceKeyValue: '06', memberKey: 'CA' }]);
    expect(pasted.unresolved.map(line => [line.lineNumber, line.reason])).toEqual([
      [2, 'Expected source_key,member_key.'],
      [3, 'Expected source_key,member_key.'],
    ]);
  });
});

describe('writeEntryLines', () => {
  it('writes the entries as lines that read back as the same entries', () => {
    const entries = [
      { mappingEntryId: 1, mappingVersionId: 40, sourceKeyValue: '003000', memberKey: 'CA' },
      { mappingEntryId: 2, mappingVersionId: 40, sourceKeyValue: '36', memberKey: 'NY' },
    ];

    const text = writeEntryLines(entries);

    expect(text).toBe('003000,CA\n36,NY');
    expect(readEntryLines(text).rows).toEqual([
      { sourceKeyValue: '003000', memberKey: 'CA' },
      { sourceKeyValue: '36', memberKey: 'NY' },
    ]);
  });
});
