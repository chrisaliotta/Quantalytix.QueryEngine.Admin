import { useForm, useWatch } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { JsonField, KeyField, NumberField, SwitchField, TextField, VocabularyField } from '@/shared/form';
import { emptyToNull } from '@/shared/text';
import type { LookupItem } from '@/shared/types';
import { usePublishedVersionItems } from '@/shared/useLatestVersionItems';
import { useLookup } from '@/shared/useLookup';
import type { ReportLineInput, ReportVersion } from './types';

/** Values of the row sheet; a choice not made yet is null, and JSON is text, empty for none. */
interface ReportRowValues {
  lineKey: string;
  displayName: string;
  lineKind: string | null;
  parentKey: string | null;
  sortOrder: number | null;
  calculatedMeasureKey: string | null;
  valueColumnKey: string | null;
  isVisible: boolean;
  payloadJson: string;
  settingsJson: string;
}

export interface ReportRowSheetProps {
  version: ReportVersion;
  /** The overlay row to edit or view; null to add one at `placement`. */
  line: ReportLineInput | null;
  /** Where a new row starts: under a member or overlay row, or at the top, after the rows already there. */
  placement: { parentKey: string | null; sortOrder: number };
  /** The rows the row can sit under: the members and the overlay rows other than itself and those below it. */
  parents: LookupItem[];
  readOnly: boolean;
  /** Saves the row into the version's overlay rows. */
  onSave: (line: ReportLineInput) => Promise<unknown>;
  onClose: () => void;
}

/**
 * Adds, edits or shows an overlay row: a section heading the rows under it, or a formula row placing a calculated measure in
 * one column. It sits under a member of the report dimension or another overlay row, among its siblings by sort order after
 * the members of equal order. Calculated measures come from the model's published version, the one the active assumption
 * set pins.
 */
export function ReportRowSheet({ version, line, placement, parents, readOnly, onSave, onClose }: ReportRowSheetProps) {
  const measures = usePublishedVersionItems('model-versions', version.modelId, 'calculated-measures');
  const columns = useLookup('report-columns', version.reportVersionId);
  const form = useForm<ReportRowValues>({
    defaultValues: {
      lineKey: line?.lineKey ?? '',
      displayName: line?.displayName ?? '',
      lineKind: line?.lineKind ?? 'section',
      parentKey: line ? (line.parentKey ?? null) : placement.parentKey,
      sortOrder: line?.sortOrder ?? placement.sortOrder,
      calculatedMeasureKey: line?.calculatedMeasureKey ?? null,
      valueColumnKey: line?.valueColumnKey ?? null,
      isVisible: line?.isVisible ?? true,
      payloadJson: line?.payloadJson ?? '',
      settingsJson: line?.settingsJson ?? '',
    },
  });
  const isFormula = useWatch({ control: form.control, name: 'lineKind' }) === 'formula';

  function submit(values: ReportRowValues) {
    return onSave({
      reportLineId: line?.reportLineId ?? null,
      lineKey: values.lineKey,
      displayName: values.displayName,
      lineKind: values.lineKind ?? '',
      parentKey: values.parentKey,
      calculatedMeasureKey: isFormula ? values.calculatedMeasureKey : null,
      valueColumnKey: isFormula ? values.valueColumnKey : null,
      sortOrder: values.sortOrder ?? 0,
      isVisible: values.isVisible,
      payloadJson: emptyToNull(values.payloadJson),
      settingsJson: emptyToNull(values.settingsJson),
    });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={line === null ? 'New row' : readOnly ? `Row ${line.lineKey}` : `Edit ${line.lineKey}`}
      description="Sections head the rows under them; formula rows are evaluated at report scope."
      form={form}
      onSubmit={submit}
      readOnly={readOnly}
    >
      <TextField name="lineKey" label="Key" description="Unique within the version, and no member key of the report dimension." />
      <TextField name="displayName" label="Name" />
      <VocabularyField name="lineKind" label="Kind" vocabulary="report_line_kind" />
      <KeyField name="parentKey" label="Under" items={parents} allowEmpty description="A member or overlay row; empty places the row at the top." />
      <NumberField name="sortOrder" label="Sort order" description="Its place among the rows under the same parent; members come first at equal order." />
      {isFormula && (
        <>
          <KeyField name="calculatedMeasureKey" label="Calculated measure" items={measures} />
          <KeyField
            name="valueColumnKey"
            label="Column"
            items={columns.data ?? null}
            description="The saved column the value is placed in; save new columns first."
          />
        </>
      )}
      <SwitchField name="isVisible" label="Visible" description="Whether the report table shows the row; the rows under a hidden row move up." />
      <JsonField name="settingsJson" label="Settings" description="Optional JSON that clients read, such as indentation or emphasis." />
      <JsonField name="payloadJson" label="Payload" description="Optional JSON that clients read; the engine passes it through." />
    </EntityFormSheet>
  );
}
