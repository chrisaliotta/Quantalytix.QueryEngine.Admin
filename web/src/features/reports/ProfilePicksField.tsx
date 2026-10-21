import { EntitySelect } from '@/shared/EntitySelect';
import { FormFieldLayout, useFormField, type FormFieldProps } from '@/shared/form';
import { useLookup } from '@/shared/useLookup';
import type { EditorView, SourceProfilePick } from './types';

export interface ProfilePicksFieldProps extends FormFieldProps {
  /** The report whose sources the picks name, over the model version whose variables they fill. */
  view: EditorView;
}

/**
 * One source picker per variable of the model's published version, each offering the report's sources that supply that variable;
 * value is the picks, which name the variable by key and the source by ID.
 */
export function ProfilePicksField({ name, label, description, disabled, view }: ProfilePicksFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  const variables = useLookup('variables', view.modelVersionId);
  const picks: SourceProfilePick[] = field.value;

  function pick(variableKey: string, reportSourceId: number | null) {
    const others = picks.filter(other => other.variableKey !== variableKey);
    field.onChange(reportSourceId === null ? others : [...others, { variableKey, reportSourceId }]);
  }

  /** The sources that supply a variable. */
  function suppliers(variableKey: string) {
    return new Set(
      view.sources
        .filter(source => source.variables.some(variable => variable.variableKey === variableKey))
        .map(source => source.source.reportSourceId)
    );
  }

  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <div data-slot="profile-picks" className="space-y-2">
        {variables.data?.map(variable => {
          const supplying = suppliers(variable.key);
          return (
            <div key={variable.key} className="grid grid-cols-[10rem_1fr] items-center gap-2 text-sm">
              <span className="truncate font-mono text-xs">{variable.key}</span>
              <EntitySelect
                lookupKind="report-sources"
                parentId={view.report.reportId}
                value={picks.find(other => other.variableKey === variable.key)?.reportSourceId ?? null}
                onChange={sourceId => pick(variable.key, sourceId)}
                filter={item => supplying.has(item.id)}
                allowEmpty
                disabled={isDisabled}
                placeholder={supplying.size === 0 ? 'No source supplies it' : 'Pick a source...'}
              />
            </div>
          );
        })}
        {variables.data?.length === 0 && <p className="text-sm text-muted-foreground">The model's published version has no variables.</p>}
      </div>
    </FormFieldLayout>
  );
}
