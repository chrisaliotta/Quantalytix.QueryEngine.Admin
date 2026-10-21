import { useForm, useWatch } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { JsonField, KeyField, SwitchField, TextField, VocabularyField } from '@/shared/form';
import { emptyToNull } from '@/shared/text';
import type { LookupItem } from '@/shared/types';
import { usePublishedVersion } from '@/shared/useLatestVersionItems';
import { useVersionContent } from '@/shared/versionContent';
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
  /** The column to edit; null to add one. */
  column: ReportColumnInput | null;
  /** Keeps the column in the tab's list; saving the list sends it. */
  onSave: (column: ReportColumnInput) => void;
  onClose: () => void;
}

/**
 * Adds or edits a column of the list being edited: the variable or calculated measure it returns, and optionally the
 * name and format it shows instead of the metric's own.
 */
export function ReportColumnSheet({ modelId, column, onSave, onClose }: ReportColumnSheetProps) {
  const model = usePublishedVersion('model-versions', modelId);
  const variables = useVersionContent('variables', model?.id ?? null);
  const measures = useVersionContent('calculated-measures', model?.id ?? null);
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
  const [valueKind, metricKey] = useWatch({ control: form.control, name: ['valueKind', 'metricKey'] });
  const isVariable = valueKind === 'variable';
  const variableItems = variables.data?.map<LookupItem>(variable => ({
    id: variable.variableId,
    key: variable.variableKey,
    label: variable.displayName,
    classifier: variable.displayFormat,
  }));
  const measureItems = measures.data?.map<LookupItem>(measure => ({
    id: measure.calculatedMeasureId,
    key: measure.measureKey,
    label: measure.displayName,
    classifier: measure.displayFormat,
  }));
  const metrics = model === null ? [] : ((isVariable ? variableItems : measureItems) ?? null);
  const metric = metrics?.find(item => item.key === metricKey);

  async function submit(values: ReportColumnValues) {
    onSave({
      reportColumnId: column?.reportColumnId ?? null,
      columnKey: values.columnKey,
      displayName: emptyToNull(values.displayName),
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
      title={column === null ? 'New column' : `Edit ${column.columnKey}`}
      description="The keys line and period name the report table's own columns, and a column key cannot be a calculation dimension's."
      form={form}
      onSubmit={submit}
      submitLabel="Keep"
    >
      <TextField name="columnKey" label="Key" description="The name of the column in the report table." />
      <VocabularyField name="valueKind" label="Returns" vocabulary="report_value_kind" />
      <KeyField name="metricKey" label={isVariable ? 'Variable' : 'Calculated measure'} items={metrics} />
      <TextField
        name="displayName"
        label="Name"
        description={metric ? `Optional; empty shows the metric's name, ${metric.label}.` : "Optional; empty shows the metric's name."}
      />
      <TextField
        name="displayFormat"
        label="Display format"
        description={metric ? `Optional; empty uses the metric's format, ${metric.classifier}.` : "Optional; empty uses the metric's format."}
      />
      <SwitchField name="isVisible" label="Visible" description="Whether a request that names no columns returns this one." />
      <JsonField name="payloadJson" label="Payload" description="Optional JSON that clients read; the engine passes it through." />
    </EntityFormSheet>
  );
}
