import { Input } from '@/components/ui/input';
import { FormFieldLayout } from './FormFieldLayout';
import { useFormField, type FormFieldProps } from './useFormField';

/** Password input for secrets such as connection strings, which the API never returns; value is a string. */
export function PasswordField({ name, label, description, disabled }: FormFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <Input
        {...field}
        id={id}
        type="password"
        autoComplete="new-password"
        value={field.value ?? ''}
        disabled={isDisabled}
        aria-invalid={error !== undefined}
      />
    </FormFieldLayout>
  );
}
