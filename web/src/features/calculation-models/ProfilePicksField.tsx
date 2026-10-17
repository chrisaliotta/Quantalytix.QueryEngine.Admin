import { EntitySelect } from '@/shared/EntitySelect';
import { FormFieldLayout, useFormField, type FormFieldProps } from '@/shared/form';
import { useModelContent } from './api';
import type { SourceProfilePick } from './types';

export interface ProfilePicksFieldProps extends FormFieldProps {
  /** The model version whose variables and sources the picks name. */
  versionId: number;
}

/** One source picker per variable of the version, each offering that variable's sources; value is the picks. */
export function ProfilePicksField({ name, label, description, disabled, versionId }: ProfilePicksFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  const variables = useModelContent('variables', versionId);
  const picks: SourceProfilePick[] = field.value;

  function pick(variableId: number, variableSourceId: number | null) {
    const others = picks.filter(other => other.variableId !== variableId);
    field.onChange(variableSourceId === null ? others : [...others, { variableId, variableSourceId }]);
  }

  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <div data-slot="profile-picks" className="space-y-2">
        {variables.data?.map(variable => (
          <div key={variable.variableId} className="grid grid-cols-[10rem_1fr] items-center gap-2 text-sm">
            <span className="truncate font-mono text-xs">{variable.variableKey}</span>
            <EntitySelect
              lookupKind="variable-sources"
              parentId={versionId}
              value={picks.find(other => other.variableId === variable.variableId)?.variableSourceId ?? null}
              onChange={sourceId => pick(variable.variableId, sourceId)}
              filter={item => item.parentId === variable.variableId}
              allowEmpty
              disabled={isDisabled}
              placeholder="Pick a source..."
            />
          </div>
        ))}
        {variables.data?.length === 0 && <p className="text-sm text-muted-foreground">The version has no variables yet.</p>}
      </div>
    </FormFieldLayout>
  );
}
