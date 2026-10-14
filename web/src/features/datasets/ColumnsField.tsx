import { MultiSelect } from '@registry/components/MultiSelect';
import { FormFieldLayout, useFormField, type FormFieldProps } from '@/shared/form';
import type { SourceColumn } from './types';

export interface ColumnsFieldProps extends FormFieldProps {
  /** The columns offered, in ordinal order. */
  columns: SourceColumn[];
}

/** Multi-select over physical columns, each shown with its StarRocks type and field type; value is the column names. */
export function ColumnsField({ name, label, description, disabled, columns }: ColumnsFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  const types = new Map(columns.map(column => [column.name, `${column.columnType} → ${column.dataType}`]));

  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <MultiSelect
        options={columns.map(column => ({ value: column.name, label: column.name }))}
        value={field.value}
        onChange={field.onChange}
        disabled={isDisabled}
        selectAll
        sort="none"
        placeholder="Select columns"
        tokens={{ popoverWidth: 'var(--radix-popover-trigger-width)' }}
        renderItemLabel={option => <ColumnOption name={option.label} type={types.get(option.value)} />}
      />
    </FormFieldLayout>
  );
}

function ColumnOption({ name, type }: { name: string; type?: string }) {
  return (
    <span className="flex w-full items-center justify-between gap-2">
      <span className="truncate">{name}</span>
      <span className="shrink-0 text-xs text-muted-foreground">{type}</span>
    </span>
  );
}
