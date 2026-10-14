import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { SwitchField, TextField } from '@/shared/form';
import { useUpdateDimension } from './api';
import type { Dimension, DimensionInput } from './types';

export interface DimensionIdentitySheetProps {
  dimension: Dimension;
  onClose: () => void;
}

/** Edits a dimension's name, availability and removability; a system dimension's removability is fixed. */
export function DimensionIdentitySheet({ dimension, onClose }: DimensionIdentitySheetProps) {
  const update = useUpdateDimension(dimension.dimensionId);
  const form = useForm<DimensionInput>({
    defaultValues: {
      displayName: dimension.displayName,
      isEnabled: dimension.isEnabled,
      isRemovable: dimension.isRemovable,
    },
  });
  const isSystem = dimension.definitionOrigin === 'system';

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={`Edit ${dimension.dimensionKey}`}
      description="Changing the name or availability refreshes the query engine's cached metadata."
      form={form}
      onSubmit={values => update.mutateAsync(values)}
    >
      <TextField name="displayName" label="Name" />
      <SwitchField name="isEnabled" label="Enabled" description="Whether queries may use the dimension." />
      <SwitchField
        name="isRemovable"
        label="Removable"
        description={isSystem ? 'System dimensions keep their removability.' : 'Whether the dimension may be deleted.'}
        disabled={isSystem}
      />
    </EntityFormSheet>
  );
}
