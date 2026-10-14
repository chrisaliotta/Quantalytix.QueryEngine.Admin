import type { ReactNode } from 'react';
import { Field, FieldDescription, FieldError, FieldLabel } from '@/components/ui/field';

export interface FormFieldLayoutProps {
  /** Id of the control the label points at. */
  id: string;
  label: string;
  description?: string;
  error?: string;
  children: ReactNode;
}

/** Label, control, description and validation message of one form field (shadcn Field). */
export function FormFieldLayout({ id, label, description, error, children }: FormFieldLayoutProps) {
  return (
    <Field data-invalid={error !== undefined}>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      {children}
      {description && <FieldDescription>{description}</FieldDescription>}
      <FieldError>{error}</FieldError>
    </Field>
  );
}
