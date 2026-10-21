/**
 * Why these tests exist: the bridge mappings bulk paste replaces every mapping of a version. Member keys are stored as keys
 * and resolve in the published versions of the bridge's dimensions, so a line is refused only for its shape or a value
 * that is no number. Factors and weights must arrive as the numbers typed, and the current mappings written as lines must
 * read back as the same mappings, so a replace keeps what the user leaves alone.
 */
import { describe, expect, it } from 'vitest';
import { readMappingLines, writeMappingLines } from './mappingLines';

describe('readMappingLines', () => {
  it('keeps the member keys and reads the factors and weights as numbers', () => {
    const pasted = readMappingLines('LOAN_A,EAST,1,0.4\nLOAN_A, WEST, -1, .6');

    expect(pasted.rows).toEqual([
      { sourceMemberKey: 'LOAN_A', targetMemberKey: 'EAST', factor: 1, allocationWeight: 0.4 },
      { sourceMemberKey: 'LOAN_A', targetMemberKey: 'WEST', factor: -1, allocationWeight: 0.6 },
    ]);
    expect(pasted.unresolved).toEqual([]);
  });

  it('lists the lines with a value that is no number or another number of values', () => {
    const pasted = readMappingLines('LOAN_B,EAST,one,1\nLOAN_B,EAST,1,50%\nLOAN_B,EAST,1');

    expect(pasted.rows).toEqual([]);
    expect(pasted.unresolved.map(line => [line.lineNumber, line.reason])).toEqual([
      [1, "The factor 'one' is not a number."],
      [2, "The weight '50%' is not a number."],
      [3, 'Expected source_key,target_key,factor,weight.'],
    ]);
  });
});

describe('writeMappingLines', () => {
  it('writes the mappings as lines that read back as the same mappings', () => {
    const mappings = [
      { bridgeMappingId: 1, bridgeVersionId: 50, sourceMemberKey: 'LOAN_A', targetMemberKey: 'EAST', factor: 1, allocationWeight: 0.086, sourceSelection: 'member' },
      { bridgeMappingId: 2, bridgeVersionId: 50, sourceMemberKey: 'LOAN_B', targetMemberKey: 'WEST', factor: -1, allocationWeight: 1, sourceSelection: 'member' },
    ];

    const text = writeMappingLines(mappings);

    expect(text).toBe('LOAN_A,EAST,1,0.086\nLOAN_B,WEST,-1,1');
    expect(readMappingLines(text).rows).toEqual([
      { sourceMemberKey: 'LOAN_A', targetMemberKey: 'EAST', factor: 1, allocationWeight: 0.086 },
      { sourceMemberKey: 'LOAN_B', targetMemberKey: 'WEST', factor: -1, allocationWeight: 1 },
    ]);
  });
});
