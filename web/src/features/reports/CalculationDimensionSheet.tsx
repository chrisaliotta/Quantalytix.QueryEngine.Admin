import { useForm, useWatch } from 'react-hook-form';
import { contentSheetTitle } from '@/shared/contentSheet';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, GroupingModeFields, SwitchField, TextField, toGroupingReferences, VocabularyField, type GroupingValues } from '@/shared/form';
import { useSaveVersionContent } from '@/shared/versionContent';
import type { CalculationDimension } from './types';

/** Values of the calculation dimension sheet; a choice not made yet is null. */
interface CalculationDimensionValues extends GroupingValues {
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
 * Creates, edits or shows a calculation dimension: a canonical dimension's members, grouped by member, level, ancestor or
 * depth, or a scalar field value. Every dataset the report reads produces it through its route.
 */
export function CalculationDimensionSheet({ versionId, dimension, readOnly, onClose }: CalculationDimensionSheetProps) {
  const save = useSaveVersionContent('calculation-dimensions', versionId, dimension?.calculationDimensionId ?? null);
  const form = useForm<CalculationDimensionValues>({
    defaultValues: {
      dimensionKey: dimension?.dimensionKey ?? '',
      displayName: dimension?.displayName ?? '',
      valueKind: dimension?.valueKind ?? 'canonical',
      dimensionId: dimension?.dimensionId ?? null,
      valueDataType: dimension?.valueDataType ?? null,
      groupingMode: dimension?.groupingMode ?? 'member',
      groupingLevelKey: dimension?.groupingLevelKey ?? null,
      groupingAncestorMemberKey: dimension?.groupingAncestorMemberKey ?? null,
      groupingDepth: dimension?.groupingDepth ?? null,
      isGroupable: dimension?.isGroupable ?? true,
      isFilterable: dimension?.isFilterable ?? true,
      isEnabled: dimension?.isEnabled ?? true,
    },
  });
  const [valueKind, dimensionId] = useWatch({ control: form.control, name: ['valueKind', 'dimensionId'] });
  const isCanonical = valueKind === 'canonical';

  function submit(values: CalculationDimensionValues) {
    const grouping = isCanonical
      ? { groupingMode: values.groupingMode, ...toGroupingReferences(values) }
      : { groupingMode: null, groupingLevelKey: null, groupingAncestorMemberKey: null, groupingDepth: null };
    return save({
      dimensionKey: values.dimensionKey,
      displayName: values.displayName,
      valueKind: values.valueKind ?? '',
      dimensionId: isCanonical ? values.dimensionId : null,
      valueDataType: isCanonical ? null : values.valueDataType,
      ...grouping,
      isGroupable: values.isGroupable,
      isFilterable: values.isFilterable,
      isEnabled: values.isEnabled,
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
      {isCanonical && (
        <>
          <EntityField name="dimensionId" label="Dimension" lookupKind="dimensions" />
          <GroupingModeFields dimensionId={dimensionId} />
        </>
      )}
      {!isCanonical && <VocabularyField name="valueDataType" label="Data type" vocabulary="data_type" />}
      <SwitchField name="isGroupable" label="Groupable" description="Whether reports may break out by it." />
      <SwitchField name="isFilterable" label="Filterable" description="Whether report filters may compare it." />
      <SwitchField name="isEnabled" label="Enabled" />
    </EntityFormSheet>
  );
}
