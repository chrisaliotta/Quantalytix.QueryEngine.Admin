import { Input } from '@/components/ui/input';
import { FormFieldLayout } from './FormFieldLayout';
import { useFormField, type FormFieldProps } from './useFormField';

/** Single-line text input; value is a string. The placeholder shows while it is empty, such as a value the row inherits. */
export function TextField({ name, label, description, disabled, placeholder }: FormFieldProps & { placeholder?: string }) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <Input
        {...field}
        id={id}
        value={field.value ?? ''}
        placeholder={placeholder}
        disabled={isDisabled}
        aria-invalid={error !== undefined}
      />
    </FormFieldLayout>
  );
}
