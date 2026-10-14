import { Textarea } from '@/components/ui/textarea';
import { FormFieldLayout } from './FormFieldLayout';
import { useFormField, type FormFieldProps } from './useFormField';

/** Multi-line text input that grows with its content; value is a string. */
export function TextAreaField({ name, label, description, disabled }: FormFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <Textarea
        {...field}
        id={id}
        value={field.value ?? ''}
        disabled={isDisabled}
        aria-invalid={error !== undefined}
      />
    </FormFieldLayout>
  );
}
