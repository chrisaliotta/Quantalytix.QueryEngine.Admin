import { useState, type BaseSyntheticEvent } from 'react';
import type { FieldValues, UseFormReturn } from 'react-hook-form';
import { showFieldErrors } from './showFieldErrors';

/**
 * Submit handler with the API error handling of `EntityFormSheet` and `FormDialog`, for any form: the validation
 * errors of an `ApiProblem` go onto their fields, and the problem is kept for an `ApiErrorAlert`. Each submit clears the
 * previous errors first, because react-hook-form keeps an error on a path no field registers, such as a list-level key,
 * and refuses every later submit while it is there.
 */
export function useProblemSubmit<TValues extends FieldValues>(
  form: UseFormReturn<TValues>,
  onSubmit: (values: TValues) => Promise<unknown>,
  onSuccess?: () => void
) {
  const [problem, setProblem] = useState<Error | null>(null);

  async function save(values: TValues) {
    setProblem(null);
    try {
      await onSubmit(values);
      onSuccess?.();
    } catch (error) {
      showFieldErrors(form, error);
      setProblem(error as Error);
    }
  }

  function submit(event?: BaseSyntheticEvent) {
    form.clearErrors();
    return form.handleSubmit(save)(event);
  }

  return { submit, problem };
}
