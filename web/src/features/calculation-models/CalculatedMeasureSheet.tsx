import { useForm } from 'react-hook-form';
import { contentSheetTitle } from '@/shared/contentSheet';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { ExpressionField, NumberField, TextField, VocabularyField } from '@/shared/form';
import { useSaveOwnedContent } from '@/shared/ownedContent';
import { emptyToNull } from '@/shared/text';
import { useLookup } from '@/shared/useLookup';
import type { CalculatedMeasure } from './types';

/** Values of the calculated measure sheet; a choice not made yet is null, and an optional text is empty for none. */
interface CalculatedMeasureValues {
  measureKey: string;
  displayName: string;
  expressionText: string;
  outputDataType: string | null;
  outputPrecision: number | null;
  outputScale: number | null;
  roundingMode: string | null;
  unitKey: string;
  displayFormat: string;
}

export interface CalculatedMeasureSheetProps {
  calculationModelId: number;
  /** The measure to edit; null to create one. */
  measure: CalculatedMeasure | null;
  onClose: () => void;
}

/**
 * Creates or edits a calculated measure. Its formula reads variables and calculated measures as `[m.KEY]`, and the
 * `@periodDays` and `@yearDays` of the period it is evaluated for.
 */
export function CalculatedMeasureSheet({ calculationModelId, measure, onClose }: CalculatedMeasureSheetProps) {
  const save = useSaveOwnedContent('calculated-measures', calculationModelId, measure?.calculatedMeasureId ?? null);
  const variables = useLookup('variables', calculationModelId);
  const measures = useLookup('calculated-measures', calculationModelId);
  const metricKeys = [...(variables.data ?? []), ...(measures.data ?? [])].map(item => item.key);
  const form = useForm<CalculatedMeasureValues>({
    defaultValues: {
      measureKey: measure?.measureKey ?? '',
      displayName: measure?.displayName ?? '',
      expressionText: measure?.expressionText ?? '',
      outputDataType: measure?.outputDataType ?? 'decimal',
      outputPrecision: measure?.outputPrecision ?? 28,
      outputScale: measure?.outputScale ?? 6,
      roundingMode: measure?.roundingMode ?? null,
      unitKey: measure?.unitKey ?? '',
      displayFormat: measure?.displayFormat ?? 'number',
    },
  });

  function submit(values: CalculatedMeasureValues) {
    return save({ ...values, outputDataType: values.outputDataType ?? '', unitKey: emptyToNull(values.unitKey) });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={contentSheetTitle('calculated measure', measure?.measureKey ?? null)}
      description="Variables and calculated measures share one key namespace. Report values are not rounded; clients round by the output scale."
      form={form}
      onSubmit={submit}
      width="40rem"
    >
      <TextField name="measureKey" label="Key" description="A letter, then letters, digits or underscores." />
      <TextField name="displayName" label="Name" />
      <ExpressionField
        name="expressionText"
        label="Formula"
        measureKeys={metricKeys}
        description="For example [m.INTEREST_INCOME] / [m.AVERAGE_BALANCE] * @yearDays / @periodDays. Division by zero gives null."
      />
      <VocabularyField name="outputDataType" label="Output type" vocabulary="measure_output_data_type" />
      <NumberField name="outputPrecision" label="Precision" description="Total digits of a decimal output." />
      <NumberField name="outputScale" label="Scale" description="Digits after the point that clients show." />
      <VocabularyField name="roundingMode" label="Rounding" vocabulary="rounding_mode" allowEmpty />
      <TextField name="unitKey" label="Unit" description="Optional, such as USD." />
      <TextField
        name="displayFormat"
        label="Display format"
        description="How clients format the value unless a report column names its own, such as percent."
      />
    </EntityFormSheet>
  );
}
