/**
 * Why these tests exist: authors write measure and binding expressions in ExpressionEditor and rely
 * on its completion to reference fields (`[f.key]`) and measures (`[m.key]`) correctly. They pin
 * down what is offered at each point of typing a reference and what gets inserted.
 */
import { describe, expect, it } from 'vitest';
import { getExpressionCompletions } from './expressionCompletion';

const fieldKeys = ['amount', 'booked_on'];
const measureKeys = ['revenue', 'margin'];

describe('getExpressionCompletions', () => {
  it('offers the field and measure prefixes after an opening bracket', () => {
    expect(getExpressionCompletions('sum([', fieldKeys, measureKeys)).toEqual([
      { label: '[f.', insertText: '[f.', replaceLength: 1, kind: 'reference' },
      { label: '[m.', insertText: '[m.', replaceLength: 1, kind: 'reference' },
    ]);
  });

  // Picking a prefix after typing `[f` must replace both characters, not append to them.
  it('replaces a partially typed prefix', () => {
    const completions = getExpressionCompletions('[f', fieldKeys, measureKeys);

    expect(completions.map(completion => completion.replaceLength)).toEqual([2, 2]);
  });

  it('offers field keys after [f. and completes the closing bracket', () => {
    expect(getExpressionCompletions('[f.', fieldKeys, measureKeys)).toEqual([
      { label: 'amount', insertText: 'amount]', replaceLength: 0, kind: 'field' },
      { label: 'booked_on', insertText: 'booked_on]', replaceLength: 0, kind: 'field' },
    ]);
  });

  it('offers measure keys after [m. and replaces the partially typed key', () => {
    expect(getExpressionCompletions('[f.amount] / [m.rev', fieldKeys, measureKeys)).toEqual([
      { label: 'revenue', insertText: 'revenue]', replaceLength: 3, kind: 'measure' },
      { label: 'margin', insertText: 'margin]', replaceLength: 3, kind: 'measure' },
    ]);
  });

  // Plain text and finished references must not keep suggesting while the author types operators.
  it('offers nothing outside a reference', () => {
    expect(getExpressionCompletions('sum(', fieldKeys, measureKeys)).toEqual([]);
    expect(getExpressionCompletions('[f.amount]', fieldKeys, measureKeys)).toEqual([]);
    expect(getExpressionCompletions('[f.amount] * 2', fieldKeys, measureKeys)).toEqual([]);
  });
});
