import { useForm } from 'react-hook-form';
import { contentSheetTitle } from '@/shared/contentSheet';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { KeyField, VocabularyField } from '@/shared/form';
import { usePublishedVersionItems } from '@/shared/useLatestVersionItems';
import type { Report, ReportVariableScopeInput, ReportVersion } from './types';

/** Values of the scope sheet; a choice not made yet is null. */
interface ReportVariableScopeValues {
  variableKey: string | null;
  scopeMemberKey: string | null;
  selectionScope: string | null;
}

export interface ReportVariableScopeSheetProps {
  report: Report;
  version: ReportVersion;
  /** The scope to edit or view; null to add one. */
  scope: ReportVariableScopeInput | null;
  readOnly: boolean;
  /** Saves the scope into the version's list. */
  onSave: (scope: ReportVariableScopeInput) => Promise<unknown>;
  onClose: () => void;
}

/**
 * Adds, edits or shows a variable scope: the member of the report's dimension whose branch a variable reads, exactly or with
 * its descendants. Variables come from the model's published version and members from the report dimension's, the versions
 * the active assumption set pins.
 */
export function ReportVariableScopeSheet({ report, version, scope, readOnly, onSave, onClose }: ReportVariableScopeSheetProps) {
  const variables = usePublishedVersionItems('model-versions', version.modelId, 'variables');
  const members = usePublishedVersionItems('dimension-versions', report.reportDimensionId, 'members');
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
      title={contentSheetTitle('scope', scope?.variableKey ?? null, readOnly)}
      description="A variable without a scope reads every row its sources classify."
      form={form}
      onSubmit={submit}
      readOnly={readOnly}
    >
      <KeyField name="variableKey" label="Variable" items={variables} description="Each variable takes one scope." />
      <KeyField name="scopeMemberKey" label="Member" items={members} description="A member of the report's dimension, such as LOANS." />
      <VocabularyField
        name="selectionScope"
        label="Selection"
        vocabulary="selection_scope"
        exclude={['all_mapped']}
        description="Exact reads the member's own rows; descendants reads its whole branch."
      />
    </EntityFormSheet>
  );
}
