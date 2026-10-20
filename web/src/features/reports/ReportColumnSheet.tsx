import { useForm, useWatch } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { JsonField, KeyField, SwitchField, TextField, VocabularyField } from '@/shared/form';
import { emptyToNull } from '@/shared/text';
import { usePublishedVersionItems } from '@/shared/useLatestVersionItems';
import type { ReportColumnInput } from './types';

/** Values of the column sheet; a choice not made yet is null, and optional text is empty for none. */
interface ReportColumnValues {
  columnKey: string;
  displayName: string;
  valueKind: string | null;
  metricKey: string | null;
  displayFormat: string;
  isVisible: boolean;
  payloadJson: string;
}

export interface ReportColumnSheetProps {
  /** The calculation model whose published version's metrics the column can return. */
  modelId: number;
  /** The column to edit or view; null to add one. */
  column: ReportColumnInput | null;
  readOnly: boolean;
  /** Keeps the column in the tab's list; saving the list sends it. */
  onSave: (column: ReportColumnInput) => void;
  onClose: () => void;
}

/** Adds, edits or shows a column of the list being edited: the variable or calculated measure it returns, and how. */
export function ReportColumnSheet({ modelId, column, readOnly, onSave, onClose }: ReportColumnSheetProps) {
  const variables = usePublishedVersionItems('model-versions', modelId, 'variables');
  const measures = usePublishedVersionItems('model-versions', modelId, 'calculated-measures');
  const form = useForm<ReportColumnValues>({
    defaultValues: {
      columnKey: column?.columnKey ?? '',
      displayName: column?.displayName ?? '',
      valueKind: column?.valueKind ?? 'variable',
      metricKey: column?.metricKey ?? null,
      displayFormat: column?.displayFormat ?? '',
      isVisible: column?.isVisible ?? true,
      payloadJson: column?.payloadJson ?? '',
    },
  });
  const isVariable = useWatch({ control: form.control, name: 'valueKind' }) === 'variable';

  async function submit(values: ReportColumnValues) {
    onSave({
      reportColumnId: column?.reportColumnId ?? null,
      columnKey: values.columnKey,
      displayName: values.displayName,
      valueKind: values.valueKind ?? '',
      metricKey: values.metricKey ?? '',
      displayFormat: emptyToNull(values.displayFormat),
      isVisible: values.isVisible,
      payloadJson: emptyToNull(values.payloadJson),
    });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={column === null ? 'New column' : readOnly ? `Column ${column.columnKey}` : `Edit ${column.columnKey}`}
      description="The keys line and period name the report table's own columns, and a column key cannot be a calculation dimension's."
      form={form}
      onSubmit={submit}
      submitLabel="Keep"
      readOnly={readOnly}
    >
      <TextField name="columnKey" label="Key" description="The name of the column in the report table." />
      <TextField name="displayName" label="Name" />
      <VocabularyField name="valueKind" label="Returns" vocabulary="report_value_kind" />
      <KeyField
        name="metricKey"
        label={isVariable ? 'Variable' : 'Calculated measure'}
        items={isVariable ? variables : measures}
      />
      <TextField name="displayFormat" label="Display format" description="Optional: how clients format the values, such as currency." />
      <SwitchField name="isVisible" label="Visible" description="Whether a request that names no columns returns this one." />
      <JsonField name="payloadJson" label="Payload" description="Optional JSON that clients read; the engine passes it through." />
    </EntityFormSheet>
  );
}
