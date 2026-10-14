import { Combobox } from '@registry/components/Combobox';
import { FormFieldLayout, useFormField, type FormFieldProps } from '@/shared/form';
import { useSourceObjects } from './queries';

export interface SourceObjectFieldProps extends FormFieldProps {
  /** The chosen `source_object_kind`; only tables or only views are offered, and nothing until it is chosen. */
  kind: string | null;
}

/** Picker over the tables or views of the tenant database; value is the object's name, or null. */
export function SourceObjectField({ name, label, description, disabled, kind }: SourceObjectFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  const sourceObjects = useSourceObjects();
  const value: string | null = field.value;
  const names = (sourceObjects.data ?? []).filter(item => item.kind === kind).map(item => item.name);
  if (value && !names.includes(value)) {
    names.unshift(value);
  }

  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <Combobox
        data={names.map(objectName => ({ value: objectName, label: objectName }))}
        value={value ?? ''}
        onValueChange={field.onChange}
        placeholder={placeholder(kind, sourceObjects.isPending)}
        searchPlaceholder="Search source objects..."
        emptyMessage="No matching source objects."
        disabled={isDisabled || kind === null || sourceObjects.isPending}
      />
      {sourceObjects.error && <p className="text-sm text-destructive">{sourceObjects.error.message}</p>}
    </FormFieldLayout>
  );
}

function placeholder(kind: string | null, isLoading: boolean) {
  if (kind === null) {
    return 'Choose the source kind first';
  }
  if (isLoading) {
    return 'Loading...';
  }
  return `Select a ${kind}`;
}
