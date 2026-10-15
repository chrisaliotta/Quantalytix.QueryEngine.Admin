import type { ComponentProps } from 'react';
import { Textarea } from '@/components/ui/textarea';
import { FormFieldLayout } from './FormFieldLayout';
import { useFormField, type FormFieldProps } from './useFormField';

/** The props of `TextAreaField`: a form field's, plus how the textarea shows its text. */
export type TextAreaFieldProps = FormFieldProps & Pick<ComponentProps<'textarea'>, 'className' | 'wrap' | 'spellCheck'>;

/**
 * Multi-line text input; value is a string. It grows with its content unless `className` caps its height, such as
 * `max-h-80 overflow-auto`, for text that can run to many lines.
 */
export function TextAreaField({ name, label, description, disabled, ...textarea }: TextAreaFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <Textarea
        {...field}
        {...textarea}
        id={id}
        value={field.value ?? ''}
        disabled={isDisabled}
        aria-invalid={error !== undefined}
      />
    </FormFieldLayout>
  );
}
