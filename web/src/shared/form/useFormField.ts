import { use, useId } from 'react';
import { useController, useFormContext } from 'react-hook-form';
import { FormReadOnlyContext } from './FormReadOnlyContext';

/** Props every shared form field accepts. */
export interface FormFieldProps {
  /** Form value path, e.g. `key` or `components.0.fieldId` (matches API validation keys). */
  name: string;
  label: string;
  description?: string;
  disabled?: boolean;
}

/**
 * Binds a field to the surrounding react-hook-form (`useFormContext`): returns the controller,
 * an element id, the error message and whether the control is disabled (also when read-only).
 */
export function useFormField(name: string, disabled = false) {
  const { control } = useFormContext();
  const { field, fieldState } = useController({ name, control });
  const readOnly = use(FormReadOnlyContext);
  const id = useId();
  return { field, id, error: fieldState.error?.message, isDisabled: disabled || readOnly };
}
