import { MultiSelect } from '@registry/components/MultiSelect';
import { lookupItemText } from '../lookupItemText';
import type { LookupItem } from '../types';
import { FormFieldLayout } from './FormFieldLayout';
import { useFormField, type FormFieldProps } from './useFormField';

export interface MultiKeyFieldProps extends FormFieldProps {
  /** The items whose keys are offered; `null` until they load, which keeps the field disabled. */
  items: LookupItem[] | null;
}

/** Picker of several keys among lookup items, for references by key, such as the inputs a driver switches; value is a string array. */
export function MultiKeyField({ name, label, description, disabled, items }: MultiKeyFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  const options = (items ?? []).map(item => ({ value: item.key, label: lookupItemText(item) }));

  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <MultiSelect
        options={options}
        value={field.value}
        onChange={field.onChange}
        isLoading={items === null}
        disabled={isDisabled}
        sort="none"
        placeholder="Select keys"
      />
    </FormFieldLayout>
  );
}
