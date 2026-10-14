import type { components } from './schema';

type RuleViolation = components['schemas']['RuleViolation'];

/** RFC 7807 fields the admin API puts in error bodies. */
interface ProblemBody {
  title?: string | null;
  detail?: string | null;
  /** Validation failures (400), keyed by camelCase JSON path, e.g. `components[0].fieldId`. */
  errors?: Record<string, string[]>;
  /** Failed rules of a version transition (422). */
  violations?: RuleViolation[];
}

/** Error thrown for every non-success API response, carrying its ProblemDetails. */
export class ApiProblem extends Error {
  readonly status: number;
  readonly title: string;
  readonly detail?: string;
  readonly errors?: Record<string, string[]>;
  readonly violations?: RuleViolation[];

  constructor(status: number, body: ProblemBody) {
    const title = body.title || `Request failed (${status})`;
    super(body.detail || title);
    this.name = 'ApiProblem';
    this.status = status;
    this.title = title;
    this.detail = body.detail ?? undefined;
    this.errors = body.errors;
    this.violations = body.violations;
  }
}

/** The parts of an openapi-fetch result that `unwrap` reads. */
interface FetchResult<TData> {
  data?: TData;
  error?: unknown;
  response: Response;
}

/**
 * Returns the data of an openapi-fetch call, or throws an `ApiProblem` for a non-success status.
 * @example const me = unwrap(await apiClient.GET('/api/me'));
 */
export function unwrap<TData>(result: FetchResult<TData>): TData {
  if (!result.response.ok) {
    throw new ApiProblem(result.response.status, toProblemBody(result.response, result.error));
  }
  return result.data as TData;
}

/**
 * Maps API validation keys to react-hook-form paths (`components[0].fieldId` -> `components.0.fieldId`),
 * joining multiple messages for the same field.
 */
export function toFieldErrors(errors: Record<string, string[]>): Record<string, string> {
  return Object.fromEntries(
    Object.entries(errors).map(([key, messages]) => [
      key.replace(/\[(\d+)\]/g, '.$1'),
      messages.join(' '),
    ])
  );
}

/** Error bodies are ProblemDetails JSON, except empty ones (e.g. a 401 from the bearer handler). */
function toProblemBody(response: Response, error: unknown): ProblemBody {
  if (typeof error === 'object' && error !== null) {
    return error as ProblemBody;
  }
  return { title: response.statusText };
}
