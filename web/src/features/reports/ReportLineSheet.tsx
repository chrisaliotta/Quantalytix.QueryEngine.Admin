import { useForm, useWatch } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { JsonField, KeyField, SwitchField, TextField, VocabularyField } from '@/shared/form';
import { emptyToNull } from '@/shared/text';
import { useLatestVersionItems } from '@/shared/useLatestVersionItems';
import { useLookup } from '@/shared/useLookup';
import type { ReportLineInput, ReportVersion } from './types';

/** Values of the line sheet; a choice not made yet is null, and JSON is text, empty for none. */
interface ReportLineValues {
  lineKey: string;
  displayName: string;
  lineKind: string | null;
  reportMemberKey: string | null;
  calculatedMeasureKey: string | null;
  valueColumnKey: string | null;
  isVisible: boolean;
  payloadJson: string;
  settingsJson: string;
}

export interface ReportLineSheetProps {
  version: ReportVersion;
  /** The line to edit or view; null to add one. */
  line: ReportLineInput | null;
  readOnly: boolean;
  /** Keeps the line in the tab's tree; saving the tree sends it. */
  onSave: (line: ReportLineInput) => void;
  onClose: () => void;
}

/**
 * Adds, edits or shows a line of the tree being edited: a section, a member line showing one report item, or a formula
 * line placing a calculated measure in one column. Member keys come from the report dimension's latest version; a
 * release checks them in the version its sources resolve.
 */
export function ReportLineSheet({ version, line, readOnly, onSave, onClose }: ReportLineSheetProps) {
  const members = useLatestVersionItems('dimension-versions', version.reportDimensionId, 'members');
  const measures = useLookup('calculated-measures', version.modelVersionId ?? null);
  const columns = useLookup('report-columns', version.reportVersionId);
  const form = useForm<ReportLineValues>({
    defaultValues: {
      lineKey: line?.lineKey ?? '',
      displayName: line?.displayName ?? '',
      lineKind: line?.lineKind ?? 'member',
      reportMemberKey: line?.reportMemberKey ?? null,
      calculatedMeasureKey: line?.calculatedMeasureKey ?? null,
      valueColumnKey: line?.valueColumnKey ?? null,
      isVisible: line?.isVisible ?? true,
      payloadJson: line?.payloadJson ?? '',
      settingsJson: line?.settingsJson ?? '',
    },
  });
  const lineKind = useWatch({ control: form.control, name: 'lineKind' });

  async function submit(values: ReportLineValues) {
    onSave({
      reportLineId: line?.reportLineId ?? null,
      lineKey: values.lineKey,
      displayName: values.displayName,
      lineKind: values.lineKind ?? '',
      reportMemberKey: lineKind === 'member' ? values.reportMemberKey : null,
      calculatedMeasureKey: lineKind === 'formula' ? values.calculatedMeasureKey : null,
      valueColumnKey: lineKind === 'formula' ? values.valueColumnKey : null,
      isVisible: values.isVisible,
      payloadJson: emptyToNull(values.payloadJson),
      settingsJson: emptyToNull(values.settingsJson),
    });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={line === null ? 'New line' : readOnly ? `Line ${line.lineKey}` : `Edit ${line.lineKey}`}
      description="Member lines show their report item's own totals; formula lines are evaluated at report scope."
      form={form}
      onSubmit={submit}
      submitLabel="Keep"
      readOnly={readOnly}
    >
      <TextField name="lineKey" label="Key" description="Unique within the version; the report table's line column shows it." />
      <TextField name="displayName" label="Name" />
      <VocabularyField name="lineKind" label="Kind" vocabulary="report_line_kind" />
      {lineKind === 'member' && <KeyField name="reportMemberKey" label="Report item" items={members} />}
      {lineKind === 'formula' && (
        <>
          <KeyField name="calculatedMeasureKey" label="Calculated measure" items={measures.data ?? null} />
          <KeyField
            name="valueColumnKey"
            label="Column"
            items={columns.data ?? null}
            description="The saved column the value is placed in; save new columns first."
          />
        </>
      )}
      <SwitchField name="isVisible" label="Visible" description="Whether the report table shows the line." />
      <JsonField name="settingsJson" label="Settings" description="Optional JSON that clients read, such as indentation or emphasis." />
      <JsonField name="payloadJson" label="Payload" description="Optional JSON that clients read; the engine passes it through." />
    </EntityFormSheet>
  );
}
