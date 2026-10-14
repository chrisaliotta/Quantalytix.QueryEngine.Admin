/**
 * Why these tests exist: the bridge mappings bulk paste resolves member keys to the member IDs the API stores. A
 * source key must resolve among the source version's members and a target key among the target version's, because
 * a member of the wrong endpoint would remap facts between unrelated members. Factors and weights must arrive as the
 * numbers typed, and the current mappings written as lines must read back as the same mappings, so a replace keeps
 * what the user leaves alone.
 */
import { describe, expect, it } from 'vitest';
import type { LookupItem } from '@/shared/types';
import { readMappingLines, writeMappingLines } from './mappingLines';

const sourceMembers: LookupItem[] = [
  { id: 1, key: 'LOAN_A', label: 'Loan A' },
  { id: 2, key: 'LOAN_B', label: 'Loan B' },
];

const targetMembers: LookupItem[] = [
  { id: 11, key: 'EAST', label: 'East' },
  { id: 12, key: 'WEST', label: 'West' },
];

describe('readMappingLines', () => {
  it('resolves source keys among the source members and target keys among the target members', () => {
    const pasted = readMappingLines('LOAN_A,EAST,1,0.4\nLOAN_A, WEST, -1, .6', sourceMembers, targetMembers);

    expect(pasted.rows).toEqual([
      { sourceMemberId: 1, targetMemberId: 11, factor: 1, allocationWeight: 0.4 },
      { sourceMemberId: 1, targetMemberId: 12, factor: -1, allocationWeight: 0.6 },
    ]);
    expect(pasted.unresolved).toEqual([]);
  });

  it('lists the lines with a key of the wrong endpoint, a value that is no number, or another number of values', () => {
    const pasted = readMappingLines(
      'EAST,LOAN_A,1,1\nLOAN_B,LOAN_A,1,1\nLOAN_B,EAST,one,1\nLOAN_B,EAST,1,50%\nLOAN_B,EAST,1',
      sourceMembers,
      targetMembers
    );

    expect(pasted.rows).toEqual([]);
    expect(pasted.unresolved.map(line => [line.lineNumber, line.reason])).toEqual([
      [1, "No member of the source version has the key 'EAST'."],
      [2, "No member of the target version has the key 'LOAN_A'."],
      [3, "The factor 'one' is not a number."],
      [4, "The weight '50%' is not a number."],
      [5, 'Expected source_key,target_key,factor,weight.'],
    ]);
  });
});

describe('writeMappingLines', () => {
  it('writes the mappings as lines that read back as the same mappings', () => {
    const mappings = [
      { bridgeVersionId: 50, sourceMemberId: 1, targetMemberId: 11, factor: 1, allocationWeight: 0.086 },
      { bridgeVersionId: 50, sourceMemberId: 2, targetMemberId: 12, factor: -1, allocationWeight: 1 },
    ];

    const text = writeMappingLines(mappings, sourceMembers, targetMembers);

    expect(text).toBe('LOAN_A,EAST,1,0.086\nLOAN_B,WEST,-1,1');
    expect(readMappingLines(text, sourceMembers, targetMembers).rows).toEqual([
      { sourceMemberId: 1, targetMemberId: 11, factor: 1, allocationWeight: 0.086 },
      { sourceMemberId: 2, targetMemberId: 12, factor: -1, allocationWeight: 1 },
    ]);
  });
});
