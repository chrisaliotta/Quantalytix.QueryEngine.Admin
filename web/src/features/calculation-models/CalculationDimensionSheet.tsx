import { useForm, useWatch } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, SwitchField, TextField, VocabularyField } from '@/shared/form';
import { useSaveModelContent } from './api';
import { contentSheetTitle } from './contentSheet';
import type { CalculationDimension } from './types';

/** Values of the calculation dimension sheet; a choice not made yet is null. */
interface CalculationDimensionValues {
  dimensionKey: string;
  displayName: string;
  valueKind: string | null;
  dimensionId: number | null;
  valueDataType: string | null;
  isGroupable: boolean;
  isFilterable: boolean;
  isEnabled: boolean;
}

export interface CalculationDimensionSheetProps {
  versionId: number;
  /** The dimension to edit or view; null to create one. */
  dimension: CalculationDimension | null;
  readOnly: boolean;
  onClose: () => void;
}

/**
 * Creates, edits or shows a calculation dimension: a canonical dimension's members, or a scalar field value, that every
 * source produces through its dataset's route.
 */
export function CalculationDimensionSheet({ versionId, dimension, readOnly, onClose }: CalculationDimensionSheetProps) {
  const save = useSaveModelContent('calculation-dimensions', versionId, dimension?.calculationDimensionId ?? null);
  const form = useForm<CalculationDimensionValues>({
    defaultValues: {
      dimensionKey: dimension?.dimensionKey ?? '',
      displayName: dimension?.displayName ?? '',
      valueKind: dimension?.valueKind ?? 'canonical',
      dimensionId: dimension?.dimensionId ?? null,
      valueDataType: dimension?.valueDataType ?? null,
      isGroupable: dimension?.isGroupable ?? true,
      isFilterable: dimension?.isFilterable ?? true,
      isEnabled: dimension?.isEnabled ?? true,
    },
  });
  const isCanonical = useWatch({ control: form.control, name: 'valueKind' }) === 'canonical';

  function submit(values: CalculationDimensionValues) {
    return save({
      ...values,
      valueKind: values.valueKind ?? '',
      dimensionId: isCanonical ? values.dimensionId : null,
      valueDataType: isCanonical ? null : values.valueDataType,
    });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={contentSheetTitle('dimension', dimension?.dimensionKey ?? null, readOnly)}
      form={form}
      onSubmit={submit}
      readOnly={readOnly}
    >
      <TextField name="dimensionKey" label="Key" description="The key report requests and defaults use; it cannot be a column key." />
      <TextField name="displayName" label="Name" />
      <VocabularyField name="valueKind" label="Value kind" vocabulary="calculation_value_kind" />
      {isCanonical && <EntityField name="dimensionId" label="Dimension" lookupKind="dimensions" />}
      {!isCanonical && <VocabularyField name="valueDataType" label="Data type" vocabulary="data_type" />}
      <SwitchField name="isGroupable" label="Groupable" description="Whether reports may break out by it." />
      <SwitchField name="isFilterable" label="Filterable" description="Whether report filters may compare it." />
      <SwitchField name="isEnabled" label="Enabled" />
    </EntityFormSheet>
  );
}
