import { EntitySelect } from '../EntitySelect';
import type { LookupItem, LookupKind } from '../types';
import { FormFieldLayout } from './FormFieldLayout';
import { useFormField, type FormFieldProps } from './useFormField';

export interface EntityFieldProps extends FormFieldProps {
  lookupKind: LookupKind;
  /** Parent id for parent-scoped kinds; `null` keeps the field disabled until the parent is chosen. */
  parentId?: number | null;
  /** Lets the user clear the value back to null. */
  allowEmpty?: boolean;
  /** Narrows the options, e.g. `item => item.classifier === 'date'`. */
  filter?: (item: LookupItem) => boolean;
}

/** Lookup picker (EntitySelect); value is the selected item id, or null. */
export function EntityField({
  name,
  label,
  description,
  disabled,
  lookupKind,
  parentId,
  allowEmpty,
  filter,
}: EntityFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <EntitySelect
        lookupKind={lookupKind}
        parentId={parentId}
        value={field.value}
        onChange={field.onChange}
        allowEmpty={allowEmpty}
        filter={filter}
        disabled={isDisabled}
      />
    </FormFieldLayout>
  );
}
