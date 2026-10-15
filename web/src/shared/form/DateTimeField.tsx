import { Input } from '@/components/ui/input';
import { fromDateTimeInput, toDateTimeInput } from '../utcTime';
import { FormFieldLayout } from './FormFieldLayout';
import { useFormField, type FormFieldProps } from './useFormField';

/** Date-time input edited in UTC, the way the API stores times; value is an ISO 8601 UTC string, or null. */
export function DateTimeField({ name, label, description, disabled }: FormFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  return (
    <FormFieldLayout id={id} label={`${label} (UTC)`} description={description} error={error}>
      <Input
        {...field}
        id={id}
        type="datetime-local"
        value={toDateTimeInput(field.value)}
        onChange={event => field.onChange(fromDateTimeInput(event.target.value))}
        disabled={isDisabled}
        aria-invalid={error !== undefined}
      />
    </FormFieldLayout>
  );
}
