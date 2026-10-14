import type { FieldValues, Path, UseFormReturn } from 'react-hook-form';
import { ApiProblem, toFieldErrors } from '@/api/problemDetails';

/**
 * Puts the field errors of an API validation problem on an inline form's fields, as `EntityFormSheet` does for sheets.
 * Other problems are left to an `ApiErrorAlert`.
 */
export function showFieldErrors<TValues extends FieldValues>(form: UseFormReturn<TValues>, error: unknown) {
  if (!(error instanceof ApiProblem) || !error.errors) {
    return;
  }
  for (const [name, message] of Object.entries(toFieldErrors(error.errors))) {
    form.setError(name as Path<TValues>, { type: 'server', message });
  }
}
