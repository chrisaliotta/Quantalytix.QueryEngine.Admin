import { Input } from '@/components/ui/input';
import { FormFieldLayout } from './FormFieldLayout';
import { useFormField, type FormFieldProps } from './useFormField';

/** Single-line text input; value is a string. */
export function TextField({ name, label, description, disabled }: FormFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <Input
        {...field}
        id={id}
        value={field.value ?? ''}
        disabled={isDisabled}
        aria-invalid={error !== undefined}
      />
    </FormFieldLayout>
  );
}
