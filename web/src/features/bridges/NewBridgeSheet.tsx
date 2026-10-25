import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, SwitchField, TextField } from '@/shared/form';
import { useCreateBridge } from './api';
import type { Bridge } from './types';

interface NewBridgeValues {
  bridgeKey: string;
  displayName: string;
  sourceDimensionId: number | null;
  isEnabled: boolean;
}

export interface NewBridgeSheetProps {
  /** The dimension whose page opened the sheet, which the bridge leads into. */
  targetDimensionId: number;
  onClose: () => void;
  onCreated: (bridge: Bridge) => void;
}

/** Creates a bridge into the dimension: its key and source dimension are fixed once created, and it starts without versions. */
export function NewBridgeSheet({ targetDimensionId, onClose, onCreated }: NewBridgeSheetProps) {
  const create = useCreateBridge();
  const form = useForm<NewBridgeValues>({
    defaultValues: { bridgeKey: '', displayName: '', sourceDimensionId: null, isEnabled: true },
  });

  async function submit(values: NewBridgeValues) {
    onCreated(await create.mutateAsync({ ...values, sourceDimensionId: values.sourceDimensionId ?? 0, targetDimensionId }));
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title="New bridge"
      description="The key and source dimension are fixed once created. Each version pins a version of each dimension, so the bridge starts without one."
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
        purpose="analytical"
        description="The dimension whose members the bridge remaps onto this one's."
      />
      <SwitchField name="isEnabled" label="Enabled" description="Whether queries may use the bridge." />
    </EntityFormSheet>
  );
}
