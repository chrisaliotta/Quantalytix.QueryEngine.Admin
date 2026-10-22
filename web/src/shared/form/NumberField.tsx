import { NumericInput } from '@registry/components/NumericInput';
import { FormFieldLayout } from './FormFieldLayout';
import { useFormField, type FormFieldProps } from './useFormField';

/** Numeric input (registry NumericInput); value is a number, or null when empty, while the placeholder shows. */
export function NumberField({ name, label, description, disabled, placeholder }: FormFieldProps & { placeholder?: string }) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <NumericInput
        {...field}
        id={id}
        placeholder={placeholder}
        disabled={isDisabled}
        error={error !== undefined}
        useGrouping={false}
      />
    </FormFieldLayout>
  );
}
