import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { SwitchField, TextField } from '@/shared/form';
import { useUpdateBridge } from './api';
import type { Bridge, BridgeInput } from './types';

export interface BridgeIdentitySheetProps {
  bridge: Bridge;
  onClose: () => void;
}

/** Edits a bridge's name and availability; its key and dimensions are fixed. */
export function BridgeIdentitySheet({ bridge, onClose }: BridgeIdentitySheetProps) {
  const update = useUpdateBridge(bridge.bridgeId);
  const form = useForm<BridgeInput>({
    defaultValues: { displayName: bridge.displayName, isEnabled: bridge.isEnabled },
  });

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={`Edit ${bridge.bridgeKey}`}
      description="Changing the name or availability refreshes the query engine's cached metadata."
      form={form}
      onSubmit={values => update.mutateAsync(values)}
    >
      <TextField name="displayName" label="Name" />
      <SwitchField name="isEnabled" label="Enabled" description="Whether queries may use the bridge." />
    </EntityFormSheet>
  );
}
