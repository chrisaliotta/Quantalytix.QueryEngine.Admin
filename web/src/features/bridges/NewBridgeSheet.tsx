import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, SwitchField, TextField } from '@/shared/form';
import { useCreateBridge } from './api';
import type { Bridge } from './types';

interface NewBridgeValues {
  bridgeKey: string;
  displayName: string;
  sourceDimensionId: number | null;
  targetDimensionId: number | null;
  isEnabled: boolean;
}

export interface NewBridgeSheetProps {
  onClose: () => void;
  onCreated: (bridge: Bridge) => void;
}

/** Creates a bridge: its key and both dimensions are fixed once created, and it starts without versions. */
export function NewBridgeSheet({ onClose, onCreated }: NewBridgeSheetProps) {
  const create = useCreateBridge();
  const form = useForm<NewBridgeValues>({
    defaultValues: {
      bridgeKey: '',
      displayName: '',
      sourceDimensionId: null,
      targetDimensionId: null,
      isEnabled: true,
    },
  });

  async function submit(values: NewBridgeValues) {
    const bridge = await create.mutateAsync({
      ...values,
      sourceDimensionId: values.sourceDimensionId ?? 0,
      targetDimensionId: values.targetDimensionId ?? 0,
    });
    onCreated(bridge);
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title="New bridge"
      description="The key and both dimensions are fixed once created. Each version pins a version of each dimension, so the bridge starts without one."
      form={form}
      onSubmit={submit}
      submitLabel="Create"
    >
      <TextField name="bridgeKey" label="Key" description="A letter, then letters, digits or underscores." />
      <TextField name="displayName" label="Name" />
      <EntityField
        name="sourceDimensionId"
        label="Source dimension"
        lookupKind="dimensions"
        description="The dimension whose members the bridge remaps."
      />
      <EntityField
        name="targetDimensionId"
        label="Target dimension"
        lookupKind="dimensions"
        description="The dimension the members map onto."
      />
      <SwitchField name="isEnabled" label="Enabled" description="Whether queries may use the bridge." />
    </EntityFormSheet>
  );
}
