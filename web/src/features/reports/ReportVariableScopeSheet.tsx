import { useForm } from 'react-hook-form';
import { contentSheetTitle } from '@/shared/contentSheet';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { KeyField, VocabularyField } from '@/shared/form';
import { usePublishedVersionItems } from '@/shared/useLatestVersionItems';
import { useLookup } from '@/shared/useLookup';
import { useReportDesign } from './designApi';
import type { ReportVariableScopeInput, ReportVersion } from './types';

/** Values of the scope sheet; a choice not made yet is null. */
interface ReportVariableScopeValues {
  variableKey: string | null;
  scopeMemberKey: string | null;
  selectionScope: string | null;
}

export interface ReportVariableScopeSheetProps {
  version: ReportVersion;
  /** The scope to edit or view; null to add one. */
  scope: ReportVariableScopeInput | null;
  readOnly: boolean;
  /** Saves the scope into the version's list. */
  onSave: (scope: ReportVariableScopeInput) => Promise<unknown>;
  onClose: () => void;
}

/**
 * Adds, edits or shows a variable scope: the row of the report whose branch a variable reads, exactly or with its descendants.
 * Variables come from the model's published version, the one the active assumption set pins, and rows from the report
 * dimension's version paired with the report version, so a draft can scope its new rows. A retired version shows its stored
 * keys only, because it has no design.
 */
export function ReportVariableScopeSheet({ version, scope, readOnly, onSave, onClose }: ReportVariableScopeSheetProps) {
  const variables = usePublishedVersionItems('model-versions', version.modelId, 'variables');
  const design = useReportDesign(version.status === 'retired' ? null : version.reportVersionId);
  const members = useLookup('members', design.data?.dimensionVersionId ?? null);
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
      <KeyField
        name="scopeMemberKey"
        label="Row"
        items={members.data ?? null}
        description="A data or group row of the report's design, such as LOANS."
      />
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
