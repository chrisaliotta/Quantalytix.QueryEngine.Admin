/**
 * Why these tests exist: every API call goes through `unwrap`, and every form maps server
 * validation errors with `toFieldErrors`. They pin down how ProblemDetails responses turn into
 * `ApiProblem`s and react-hook-form field paths, which all feature pages rely on.
 */
import { describe, expect, it } from 'vitest';
import { ApiProblem, toFieldErrors, unwrap } from './problemDetails';

function response(status: number, statusText = '') {
  return new Response(null, { status, statusText });
}

describe('unwrap', () => {
  it('returns the data of a successful response', () => {
    const result = { data: { tenantId: 'sandbox' }, response: response(200) };

    expect(unwrap(result)).toEqual({ tenantId: 'sandbox' });
  });

  // Forms rely on `errors` to highlight fields after a 400.
  it('throws an ApiProblem carrying validation errors', () => {
    const result = {
      error: {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: { key: ['Key is required.'] },
      },
      response: response(400),
    };

    const problem = captureProblem(() => unwrap(result));

    expect(problem).toBeInstanceOf(ApiProblem);
    expect(problem.status).toBe(400);
    expect(problem.title).toBe('One or more validation errors occurred.');
    expect(problem.errors).toEqual({ key: ['Key is required.'] });
  });

  // VersionHeader lists the failed rules of a rejected transition.
  it('throws an ApiProblem carrying rule violations', () => {
    const result = {
      error: {
        title: 'Version is not valid.',
        detail: '1 rule failed.',
        status: 422,
        violations: [{ rule: 'grain.required', message: 'A grain is required.' }],
      },
      response: response(422),
    };

    const problem = captureProblem(() => unwrap(result));

    expect(problem.detail).toBe('1 rule failed.');
    expect(problem.message).toBe('1 rule failed.');
    expect(problem.violations).toEqual([{ rule: 'grain.required', message: 'A grain is required.' }]);
  });

  // The bearer handler answers 401 with an empty body; the problem still needs a readable title.
  it('builds a problem from the status when the error body is empty', () => {
    const problem = captureProblem(() => unwrap({ response: response(401, 'Unauthorized') }));

    expect(problem.status).toBe(401);
    expect(problem.title).toBe('Unauthorized');
  });
});

describe('toFieldErrors', () => {
  // The API keys errors by JSON path; react-hook-form addresses array items with dots.
  it('converts indexer paths to react-hook-form paths', () => {
    const fieldErrors = toFieldErrors({
      name: ['Name is required.'],
      'components[0].fieldId': ['Field does not exist.'],
      'levels[2].members[10].key': ['Key must be unique.'],
    });

    expect(fieldErrors).toEqual({
      name: 'Name is required.',
      'components.0.fieldId': 'Field does not exist.',
      'levels.2.members.10.key': 'Key must be unique.',
    });
  });

  it('joins several messages for the same field', () => {
    expect(toFieldErrors({ key: ['Key is required.', 'Key must be lowercase.'] })).toEqual({
      key: 'Key is required. Key must be lowercase.',
    });
  });
});

function captureProblem(action: () => unknown): ApiProblem {
  try {
    action();
  } catch (error) {
    return error as ApiProblem;
  }
  throw new Error('Expected an ApiProblem to be thrown.');
}
