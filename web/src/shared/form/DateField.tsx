import { format, parse } from 'date-fns';
import { useMemo } from 'react';
import { DatePicker, type DatePickerValue } from '@registry/components/DatePicker';
import { FormFieldLayout } from './FormFieldLayout';
import { useFormField, type FormFieldProps } from './useFormField';

const ISO_DATE = 'yyyy-MM-dd';

/** Date picker (registry DatePicker); value is an ISO date string (`yyyy-MM-dd`), or null. */
export function DateField({ name, label, description, disabled }: FormFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  const value = useMemo(() => toPickerValue(field.value), [field.value]);
  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <DatePicker
        id={id}
        name={field.name}
        value={value}
        onValueChange={next => field.onChange(toIsoDate(next))}
        disabled={isDisabled}
        restoreOnEmpty={false}
      />
    </FormFieldLayout>
  );
}

function toPickerValue(isoDate: string | null): DatePickerValue | undefined {
  if (!isoDate) {
    return undefined;
  }
  return { date: parse(isoDate, ISO_DATE, new Date()) };
}

function toIsoDate(value: DatePickerValue | undefined): string | null {
  if (!value) {
    return null;
  }
  return format(value.date, ISO_DATE);
}
