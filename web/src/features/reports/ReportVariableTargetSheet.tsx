import { useForm } from 'react-hook-form';
import { contentSheetTitle } from '@/shared/contentSheet';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { KeyField, VocabularyField } from '@/shared/form';
import { usePublishedVersionItems } from '@/shared/useLatestVersionItems';
import type { Report, ReportVariableTargetInput, ReportVersion } from './types';

/** Values of the target sheet; a choice not made yet is null. */
interface ReportVariableTargetValues {
  variableKey: string | null;
  targetMemberKey: string | null;
  selectionScope: string | null;
}

export interface ReportVariableTargetSheetProps {
  report: Report;
  version: ReportVersion;
  /** The target to edit or view; null to add one. */
  target: ReportVariableTargetInput | null;
  readOnly: boolean;
  /** Saves the target into the version's list. */
  onSave: (target: ReportVariableTargetInput) => Promise<unknown>;
  onClose: () => void;
}

/**
 * Adds, edits or shows a variable target: the member of the report's dimension a variable reads at report scope, exactly or
 * with its descendants. Variables come from the model's published version and members from the report dimension's, the
 * versions the active assumption set pins.
 */
export function ReportVariableTargetSheet({ report, version, target, readOnly, onSave, onClose }: ReportVariableTargetSheetProps) {
  const variables = usePublishedVersionItems('model-versions', version.modelId, 'variables');
  const members = usePublishedVersionItems('dimension-versions', report.reportDimensionId, 'members');
  const form = useForm<ReportVariableTargetValues>({
    defaultValues: {
      variableKey: target?.variableKey ?? null,
      targetMemberKey: target?.targetMemberKey ?? null,
      selectionScope: target?.selectionScope ?? 'descendants',
    },
  });

  function submit(values: ReportVariableTargetValues) {
    return onSave({
      variableKey: values.variableKey ?? '',
      targetMemberKey: values.targetMemberKey ?? '',
      selectionScope: values.selectionScope ?? '',
    });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={contentSheetTitle('target', target?.variableKey ?? null, readOnly)}
      description="A variable without a target reads every row its sources classify."
      form={form}
      onSubmit={submit}
      readOnly={readOnly}
    >
      <KeyField name="variableKey" label="Variable" items={variables} description="Each variable takes one target." />
      <KeyField name="targetMemberKey" label="Target member" items={members} description="A member of the report's dimension, such as LOANS." />
      <VocabularyField name="selectionScope" label="Scope" vocabulary="selection_scope" exclude={['all_mapped']} />
    </EntityFormSheet>
  );
}
