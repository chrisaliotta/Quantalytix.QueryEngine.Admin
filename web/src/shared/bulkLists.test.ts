/**
 * Why these tests exist: bulk paste turns free text into the list a bulk replace stores, so the reader must keep the
 * values as typed and report each line it cannot use by its line number. The API answers a bad item by its list index,
 * which a paste form has no field for: the errors must name the pasted line instead, and must stay off form paths that
 * react-hook-form would keep, because a kept error on such a path blocks every later submit of the form.
 */
import { describe, expect, it } from 'vitest';
import { ApiProblem } from '@/api/problemDetails';
import { problemOnItem, problemOnLines, readPastedLines } from './bulkLists';

describe('readPastedLines', () => {
  it('skips blank lines, trims the values and numbers the lines as pasted', () => {
    const pasted = readPastedLines('003000 , CA\r\n\n  \n3000,NY\nbad', values => {
      if (values.length !== 2) {
        return { reason: 'Expected two values.' };
      }
      return { row: values };
    });

    expect(pasted.rows).toEqual([
      ['003000', 'CA'],
      ['3000', 'NY'],
    ]);
    expect(pasted.lineNumbers).toEqual([1, 4]);
    expect(pasted.unresolved).toEqual([{ lineNumber: 5, text: 'bad', reason: 'Expected two values.' }]);
  });
});

describe('problemOnLines', () => {
  it('names the pasted line of each item error and keeps the list errors, all on one field', () => {
    const problem = new ApiProblem(400, {
      title: 'One or more validation errors occurred.',
      errors: {
        'entries[1].sourceKeyValue': ["'sourceKeyValue' must not be empty."],
        entries: ["Source key '06' appears more than once."],
      },
    });

    const placed = problemOnLines(problem, 'entries', [2, 5], 'text') as ApiProblem;

    expect(placed.status).toBe(400);
    expect(placed.errors).toEqual({
      text: ["Line 5: 'sourceKeyValue' must not be empty.", "Source key '06' appears more than once."],
    });
  });

  it('passes problems without validation errors through', () => {
    const conflict = new ApiProblem(409, { title: 'Version is not a draft' });

    expect(problemOnLines(conflict, 'entries', [], 'text')).toBe(conflict);
  });
});

describe('problemOnItem', () => {
  it("puts the edited item's errors on its fields and every other error under root", () => {
    const problem = new ApiProblem(400, {
      title: 'One or more validation errors occurred.',
      errors: {
        'mappings[2].allocationWeight': ["'allocationWeight' must be greater than or equal to '0'."],
        mappings: ['Source member 1 is mapped to target member 11 more than once.'],
      },
    });

    const placed = problemOnItem(problem, 'mappings', 2) as ApiProblem;

    expect(placed.errors).toEqual({
      allocationWeight: ["'allocationWeight' must be greater than or equal to '0'."],
      'root.mappings': ['Source member 1 is mapped to target member 11 more than once.'],
    });
  });
});
