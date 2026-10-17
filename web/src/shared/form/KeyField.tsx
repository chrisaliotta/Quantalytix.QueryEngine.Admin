import { Combobox } from '@registry/components/Combobox';
import { ClearSelectionButton } from '../ClearSelectionButton';
import { lookupItemText } from '../lookupItemText';
import type { LookupItem } from '../types';
import { FormFieldLayout } from './FormFieldLayout';
import { useFormField, type FormFieldProps } from './useFormField';

export interface KeyFieldProps extends FormFieldProps {
  /** The items whose keys are offered; `null` until their owner is chosen and loaded, which keeps the field disabled. */
  items: LookupItem[] | null;
  /** Lets the user clear the value back to null. */
  allowEmpty?: boolean;
}

/**
 * Picker of one key among lookup items, for references by key rather than ID, such as a source's measure key; value is
 * the key text, or null. A key the items do not list, such as one a later version dropped, is shown as it is.
 */
export function KeyField({ name, label, description, disabled, items, allowEmpty }: KeyFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  const value: string | null = field.value;
  const options = (items ?? []).map(item => ({ value: item.key, label: lookupItemText(item) }));

  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <div className="flex items-center gap-1">
        <Combobox
          className="min-w-0 flex-1"
          data={options}
          value={value ?? ''}
          onValueChange={field.onChange}
          selectedItemRenderer={item => item?.label ?? value}
          placeholder={items === null ? 'Choose the owner first' : 'Select...'}
          emptyMessage="No matching keys."
          disabled={isDisabled || items === null}
        />
        {allowEmpty && value !== null && !isDisabled && <ClearSelectionButton onClick={() => field.onChange(null)} />}
      </div>
    </FormFieldLayout>
  );
}
