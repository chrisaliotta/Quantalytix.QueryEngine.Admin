import { useEffect, useRef } from 'react';
import { useForm, useWatch, type UseFormReturn } from 'react-hook-form';
import { contentSheetTitle } from '@/shared/contentSheet';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, GroupingModeFields, SwitchField, TextField, toGroupingReferences, type GroupingValues } from '@/shared/form';
import { useSaveOwnedContent } from '@/shared/ownedContent';
import { useLookup } from '@/shared/useLookup';
import type { CalculationDimension } from './types';

/** Values of the calculation dimension sheet; a choice not made yet is null. */
interface CalculationDimensionValues extends GroupingValues {
  dimensionKey: string;
  displayName: string;
  dimensionRoleId: number | null;
  isGroupable: boolean;
  isFilterable: boolean;
  isEnabled: boolean;
}

export interface CalculationDimensionSheetProps {
  reportId: number;
  /** The dimension to edit; null to create one. */
  dimension: CalculationDimension | null;
  onClose: () => void;
}

/**
 * Creates or edits a calculation dimension: a role, whose binding each source's dataset classifies rows by, with its
 * template's members grouped by member, level, ancestor or depth. Levels and members come from the template's published version.
 */
export function CalculationDimensionSheet({ reportId, dimension, onClose }: CalculationDimensionSheetProps) {
  const save = useSaveOwnedContent('calculation-dimensions', reportId, dimension?.calculationDimensionId ?? null);
  const roles = useLookup('dimension-roles');
  const form = useForm<CalculationDimensionValues>({
    defaultValues: {
      dimensionKey: dimension?.dimensionKey ?? '',
      displayName: dimension?.displayName ?? '',
      dimensionRoleId: dimension?.dimensionRoleId ?? null,
      groupingMode: dimension?.groupingMode ?? 'member',
      groupingLevelKey: dimension?.groupingLevelKey ?? null,
      groupingAncestorMemberKey: dimension?.groupingAncestorMemberKey ?? null,
      groupingDepth: dimension?.groupingDepth ?? null,
      isGroupable: dimension?.isGroupable ?? true,
      isFilterable: dimension?.isFilterable ?? true,
      isEnabled: dimension?.isEnabled ?? true,
    },
  });
  const dimensionRoleId = useWatch({ control: form.control, name: 'dimensionRoleId' });
  const templateId = roles.data?.find(role => role.id === dimensionRoleId)?.parentId ?? null;
  useGroupingKeysClearing(form, templateId);

  function submit(values: CalculationDimensionValues) {
    return save({
      dimensionKey: values.dimensionKey,
      displayName: values.displayName,
      dimensionRoleId: values.dimensionRoleId ?? 0,
      groupingMode: values.groupingMode ?? '',
      ...toGroupingReferences(values),
      isGroupable: values.isGroupable,
      isFilterable: values.isFilterable,
      isEnabled: values.isEnabled,
    });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={contentSheetTitle('dimension', dimension?.dimensionKey ?? null)}
      form={form}
      onSubmit={submit}
    >
      <TextField name="dimensionKey" label="Key" description="The key report requests and defaults use; it cannot be a column key." />
      <TextField name="displayName" label="Name" />
      <EntityField
        name="dimensionRoleId"
        label="Role"
        lookupKind="dimension-roles"
        description="Each source reaches the dimension through its dataset's binding of the role; a column breaks out by it only when every source it reads does."
      />
      <GroupingModeFields dimensionId={templateId} />
      <SwitchField name="isGroupable" label="Groupable" description="Whether reports may break out by it." />
      <SwitchField name="isFilterable" label="Filterable" description="Whether report filters may compare it." />
      <SwitchField name="isEnabled" label="Enabled" />
    </EntityFormSheet>
  );
}

/**
 * Clears the level and ancestor keys once the role changes to one of another template, because they name the old template's
 * members. It runs after render, never inside a form notification, so watchers keep seeing the cleared values.
 */
function useGroupingKeysClearing(form: UseFormReturn<CalculationDimensionValues>, templateId: number | null) {
  const previous = useRef(templateId);
  useEffect(() => {
    const before = previous.current;
    previous.current = templateId;
    if (before !== null && templateId !== before) {
      form.setValue('groupingLevelKey', null);
      form.setValue('groupingAncestorMemberKey', null);
    }
  }, [form, templateId]);
}
