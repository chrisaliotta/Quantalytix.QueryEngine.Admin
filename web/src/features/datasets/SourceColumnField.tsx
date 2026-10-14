import { Combobox } from '@registry/components/Combobox';
import { ClearSelectionButton } from '@/shared/ClearSelectionButton';
import { FormFieldLayout, useFormField, type FormFieldProps } from '@/shared/form';
import { useSourceColumns } from './queries';
import type { SourceColumn } from './types';

export interface SourceColumnFieldProps extends FormFieldProps {
  /** The table or view whose columns are offered. */
  sourceObject: string;
  /** Lets the user clear the value back to null. */
  allowEmpty?: boolean;
  /** Called with the column the user picks, e.g. to take over its field type. */
  onColumnSelected?: (column: SourceColumn) => void;
}

/** Picker over the columns of a source object, labelled with their StarRocks types; value is the column name, or null. */
export function SourceColumnField({
  name,
  label,
  description,
  disabled,
  sourceObject,
  allowEmpty = false,
  onColumnSelected,
}: SourceColumnFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  const columns = useSourceColumns(sourceObject);
  const value: string | null = field.value;
  const items = (columns.data ?? []).map(column => ({
    value: column.name,
    label: `${column.name} — ${column.columnType}`,
  }));
  if (value && !items.some(item => item.value === value)) {
    items.unshift({ value, label: value });
  }

  function select(columnName: string) {
    field.onChange(columnName);
    const column = columns.data?.find(item => item.name === columnName);
    if (column && onColumnSelected) {
      onColumnSelected(column);
    }
  }

  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <div className="flex items-center gap-1">
        <Combobox
          className="min-w-0 flex-1"
          data={items}
          value={value ?? ''}
          onValueChange={select}
          placeholder={columns.isPending ? 'Loading...' : 'Select a column'}
          searchPlaceholder="Search columns..."
          emptyMessage="No matching columns."
          sort="none"
          disabled={isDisabled || columns.isPending}
        />
        {allowEmpty && value !== null && !isDisabled && (
          <ClearSelectionButton onClick={() => field.onChange(null)} />
        )}
      </div>
      {columns.error && <p className="text-sm text-destructive">{columns.error.message}</p>}
    </FormFieldLayout>
  );
}
