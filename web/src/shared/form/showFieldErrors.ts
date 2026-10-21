import type { FieldValues, Path, UseFormReturn } from 'react-hook-form';
import { ApiProblem, toFieldErrors } from '@/api/problemDetails';

/**
 * Puts the field errors of an API validation problem on a form's fields. Other problems are left to an
 * `ApiErrorAlert`. `fieldPath` maps an error's path to the field it belongs on, for a form whose fields are not the body it sends.
 */
export function showFieldErrors<TValues extends FieldValues>(
  form: UseFormReturn<TValues>,
  error: unknown,
  fieldPath: (path: string) => string = path => path
) {
  if (!(error instanceof ApiProblem) || !error.errors) {
    return;
  }
  for (const [name, message] of Object.entries(toFieldErrors(error.errors))) {
    form.setError(fieldPath(name) as Path<TValues>, { type: 'server', message });
  }
}
