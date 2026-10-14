import { useState } from 'react';
import type { FieldValues, UseFormReturn } from 'react-hook-form';
import { showFieldErrors } from './showFieldErrors';

/**
 * Submit handler with the API error handling of `EntityFormSheet` and `FormDialog`, for any form: the validation
 * errors of an `ApiProblem` go onto their fields, and the problem is kept for an `ApiErrorAlert`.
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
      showFieldErrors(form, error);
      setProblem(error as Error);
    }
  }

  return { submit: form.handleSubmit(submit), problem };
}
