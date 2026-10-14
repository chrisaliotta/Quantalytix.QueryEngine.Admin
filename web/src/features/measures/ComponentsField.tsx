import { Plus, X } from 'lucide-react';
import { get, useFormState, type FieldErrors } from 'react-hook-form';
import { Combobox } from '@registry/components/Combobox';
import { Button } from '@/components/ui/button';
import { EntitySelect } from '@/shared/EntitySelect';
import { FormFieldLayout, useFormField, type FormFieldProps } from '@/shared/form';
import { useVocabulary } from '@/shared/useVocabularies';
import { VocabularyOption } from '@/shared/VocabularySelect';
import type { ComponentFormValues } from './measureForm';

export interface ComponentsFieldProps extends FormFieldProps {
  /** The dataset version whose fields supply the roles. */
  versionId: number;
  /** The roles the measure's kind reads, in the order to offer them. */
  roles: string[];
}

/**
 * Editor of a measure's components: one row per role with the field of the version that supplies it. It offers each
 * role the measure's kind reads once; the value is the list of rows, and each row shows the API's errors for its role
 * and field under it.
 */
export function ComponentsField({ name, label, description, disabled, versionId, roles }: ComponentsFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  const { errors } = useFormState({ name });
  const vocabulary = useVocabulary('component_role');
  const descriptions = new Map(vocabulary.data?.values.map(option => [option.value, option.description]));
  const rows: ComponentFormValues[] = field.value;
  const unused = roles.filter(role => !rows.some(row => row.componentRole === role));

  function change(index: number, next: Partial<ComponentFormValues>) {
    field.onChange(rows.map((row, at) => (at === index ? { ...row, ...next } : row)));
  }

  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <ul data-slot="measure-components" className="space-y-2">
        {rows.map((row, index) => (
          <li key={row.componentRole} className="space-y-1">
            <div className="flex items-center gap-2">
              <Combobox
                className="w-40 shrink-0"
                data={[row.componentRole, ...unused].map(role => ({ value: role, label: role }))}
                value={row.componentRole}
                onValueChange={role => change(index, { componentRole: role })}
                placeholder="Role"
                emptyMessage="No other roles."
                disabled={isDisabled}
                sort="none"
                itemRenderer={item => <VocabularyOption value={item.value} description={descriptions.get(item.value)} />}
              />
              <div className="min-w-0 flex-1">
                <EntitySelect
                  lookupKind="fields"
                  parentId={versionId}
                  value={row.fieldId}
                  onChange={fieldId => change(index, { fieldId })}
                  disabled={isDisabled}
                  placeholder="Choose a field..."
                />
              </div>
              {!isDisabled && (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-xs"
                  aria-label={`Remove the ${row.componentRole} component`}
                  onClick={() => field.onChange(rows.filter((_, at) => at !== index))}
                >
                  <X />
                </Button>
              )}
            </div>
            <RowErrors errors={errors} path={`${name}.${index}`} />
          </li>
        ))}
      </ul>
      {!isDisabled && unused.length > 0 && (
        <Button
          type="button"
          variant="outline"
          size="sm"
          className="self-start"
          onClick={() => field.onChange([...rows, { componentRole: unused[0], fieldId: null }])}
        >
          <Plus />
          Add {unused[0]} component
        </Button>
      )}
    </FormFieldLayout>
  );
}

/** The API's errors for one component row's role and field. */
function RowErrors({ errors, path }: { errors: FieldErrors; path: string }) {
  const messages = ['componentRole', 'fieldId']
    .map(part => get(errors, `${path}.${part}`)?.message as string | undefined)
    .filter(message => message !== undefined);
  if (messages.length === 0) {
    return null;
  }
  return <p className="text-sm text-destructive">{messages.join(' ')}</p>;
}
