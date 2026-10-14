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

/** Sheet that edits a draft assumption set's display name, description and effective time. */
export function AssumptionSetSettingsSheet({ assumptionSetId, form, open, onOpenChange }: AssumptionSetSettingsSheetProps) {
  const update = useUpdateAssumptionSet(assumptionSetId);
  return (
    <EntityFormSheet
      open={open}
      onOpenChange={onOpenChange}
      title="Assumption set settings"
      description="Changing the effective time does not change existing pins; capture again to refresh them."
      form={form}
      onSubmit={values => update.mutateAsync({ ...values, description: values.description || null })}
    >
      <AssumptionSetSettingsFields />
    </EntityFormSheet>
  );
}
