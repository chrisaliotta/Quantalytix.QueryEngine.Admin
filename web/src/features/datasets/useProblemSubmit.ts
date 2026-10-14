import { useState } from 'react';
import type { FieldValues, Path, UseFormReturn } from 'react-hook-form';
import { ApiProblem, toFieldErrors } from '@/api/problemDetails';

/**
 * Submit handler for a form outside EntityFormSheet, with its error handling: the validation errors of an
 * `ApiProblem` go onto their fields, and the problem is kept for an `ApiErrorAlert`.
 */
export function useProblemSubmit<TValues extends FieldValues>(
  form: UseFormReturn<TValues>,
  onSubmit: (values: TValues) => Promise<unknown>,
  onSuccess?: () => void
) {
  const [problem, setProblem] = useState<Error | null>(null);

  async function submit(values: TValues) {
    setProblem(null);
    try {
      await onSubmit(values);
      onSuccess?.();
    } catch (error) {
      if (error instanceof ApiProblem && error.errors) {
        for (const [name, message] of Object.entries(toFieldErrors(error.errors))) {
          form.setError(name as Path<TValues>, { type: 'server', message });
        }
      }
      setProblem(error as Error);
    }
  }

  return { submit: form.handleSubmit(submit), problem };
}
