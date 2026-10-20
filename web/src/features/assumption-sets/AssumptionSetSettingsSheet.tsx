import type { UseFormReturn } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { AssumptionSetSettingsFields } from './AssumptionSetSettingsFields';
import type { AssumptionSetUpdateInput } from './types';
import { useUpdateAssumptionSet } from './useAssumptionSets';

export interface AssumptionSetSettingsSheetProps {
  assumptionSetId: number;
  form: UseFormReturn<AssumptionSetUpdateInput>;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/** Sheet that relabels an assumption set: its display name and description. */
export function AssumptionSetSettingsSheet({ assumptionSetId, form, open, onOpenChange }: AssumptionSetSettingsSheetProps) {
  const update = useUpdateAssumptionSet(assumptionSetId);
  return (
    <EntityFormSheet
      open={open}
      onOpenChange={onOpenChange}
      title="Edit labels"
      description="The key and the pins stay as they are."
      form={form}
      onSubmit={values => update.mutateAsync({ ...values, description: values.description || null })}
    >
      <AssumptionSetSettingsFields />
    </EntityFormSheet>
  );
}
