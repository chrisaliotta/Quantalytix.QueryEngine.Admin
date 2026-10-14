import { format } from 'date-fns';
import { Input } from '@/components/ui/input';
import { FormFieldLayout } from './FormFieldLayout';
import { useFormField, type FormFieldProps } from './useFormField';

/** Date-time input edited in local time; value is an ISO 8601 UTC string, or null. */
export function DateTimeField({ name, label, description, disabled }: FormFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <Input
        {...field}
        id={id}
        type="datetime-local"
        value={toLocalInput(field.value)}
        onChange={event => field.onChange(toUtcIso(event.target.value))}
        disabled={isDisabled}
        aria-invalid={error !== undefined}
      />
    </FormFieldLayout>
  );
}

function toLocalInput(utcIso: string | null): string {
  if (!utcIso) {
    return '';
  }
  return format(new Date(utcIso), "yyyy-MM-dd'T'HH:mm");
}

function toUtcIso(localValue: string): string | null {
  if (!localValue) {
    return null;
  }
  return new Date(localValue).toISOString();
}
