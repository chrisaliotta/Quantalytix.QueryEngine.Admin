import { useForm } from 'react-hook-form';
import { contentSheetTitle } from '@/shared/contentSheet';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { KeyField, VocabularyField } from '@/shared/form';
import { useLookup } from '@/shared/useLookup';
import type { EditorView, ReportVariableScopeInput } from './types';

/** Values of the scope sheet; a choice not made yet is null. */
interface ReportVariableScopeValues {
  variableKey: string | null;
  scopeMemberKey: string | null;
  selectionScope: string | null;
}

export interface ReportVariableScopeSheetProps {
  view: EditorView;
  /** The scope to edit; null to add one. */
  scope: ReportVariableScopeInput | null;
  /** Saves the scope into the report's list. */
  onSave: (scope: ReportVariableScopeInput) => Promise<unknown>;
  onClose: () => void;
}

/**
 * Adds or edits a variable scope: the row of the report whose branch a variable reads, exactly or with its descendants. Variables
 * come from the report's model, and rows from the report dimension's one version.
 */
export function ReportVariableScopeSheet({ view, scope, onSave, onClose }: ReportVariableScopeSheetProps) {
  const variables = useLookup('variables', view.report.calculationModelId);
  const members = useLookup('members', view.dimensionVersionId);
  const form = useForm<ReportVariableScopeValues>({
    defaultValues: {
      variableKey: scope?.variableKey ?? null,
      scopeMemberKey: scope?.scopeMemberKey ?? null,
      selectionScope: scope?.selectionScope ?? 'descendants',
    },
  });

  function submit(values: ReportVariableScopeValues) {
    return onSave({
      variableKey: values.variableKey ?? '',
      scopeMemberKey: values.scopeMemberKey ?? '',
      selectionScope: values.selectionScope ?? '',
    });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={contentSheetTitle('scope', scope?.variableKey ?? null)}
      description="A variable without a scope reads every row its sources reach."
      form={form}
      onSubmit={submit}
    >
      <KeyField name="variableKey" label="Variable" items={variables.data ?? null} description="Each variable takes one scope." />
      <KeyField name="scopeMemberKey" label="Row" items={members.data ?? null} description="A data or group row of the report, such as LOANS." />
      <VocabularyField
        name="selectionScope"
        label="Selection"
        vocabulary="selection_scope"
        exclude={['all_mapped']}
        description="Exact reads the row's own items; descendants reads its whole branch."
      />
    </EntityFormSheet>
  );
}
