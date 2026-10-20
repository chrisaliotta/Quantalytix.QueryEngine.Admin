import { EntitySelect } from '@/shared/EntitySelect';
import { FormFieldLayout, useFormField, type FormFieldProps } from '@/shared/form';
import { usePublishedVersionItems } from '@/shared/useLatestVersionItems';
import type { ReportVersion, SourceProfilePick } from './types';

export interface ProfilePicksFieldProps extends FormFieldProps {
  /** The report version whose sources the picks name, over the model whose variables they fill. */
  version: ReportVersion;
}

/**
 * One source picker per variable of the model's published version, each offering the version's sources of that variable;
 * value is the picks, which name the variable by key and the source by ID.
 */
export function ProfilePicksField({ name, label, description, disabled, version }: ProfilePicksFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  const variables = usePublishedVersionItems('model-versions', version.modelId, 'variables');
  const picks: SourceProfilePick[] = field.value;

  function pick(variableKey: string, variableSourceId: number | null) {
    const others = picks.filter(other => other.variableKey !== variableKey);
    field.onChange(variableSourceId === null ? others : [...others, { variableKey, variableSourceId }]);
  }

  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <div data-slot="profile-picks" className="space-y-2">
        {variables?.map(variable => (
          <div key={variable.key} className="grid grid-cols-[10rem_1fr] items-center gap-2 text-sm">
            <span className="truncate font-mono text-xs">{variable.key}</span>
            <EntitySelect
              lookupKind="variable-sources"
              parentId={version.reportVersionId}
              value={picks.find(other => other.variableKey === variable.key)?.variableSourceId ?? null}
              onChange={sourceId => pick(variable.key, sourceId)}
              filter={item => item.classifier === variable.key}
              allowEmpty
              disabled={isDisabled}
              placeholder="Pick a source..."
            />
          </div>
        ))}
        {variables?.length === 0 && (
          <p className="text-sm text-muted-foreground">The model has no published version with variables yet.</p>
        )}
      </div>
    </FormFieldLayout>
  );
}
