import { useForm } from 'react-hook-form';
import { EntityField } from '@/shared/form';
import { FormDialog } from '@/shared/FormDialog';
import { useCreateBridgeVersion } from './api';
import { toBridgeVersionInput, type BridgeVersionValues } from './bridgeVersionForm';
import { BridgeVersionFields } from './BridgeVersionFields';
import type { Bridge, CreatedBridgeVersion } from './types';

/** Values of the new-version dialog: the version's attributes and the optional clone source. */
export interface NewBridgeVersionValues extends BridgeVersionValues {
  cloneFromVersionId: number | null;
}

export interface NewBridgeVersionDialogProps {
  bridge: Bridge;
  /** The values the fields start with: a clone source's attributes, or defaults for an empty draft. */
  defaults: NewBridgeVersionValues;
  onClose: () => void;
  onCreated: (created: CreatedBridgeVersion) => void;
}

/**
 * Creates a draft bridge version: its endpoint versions and allocation semantics, and optionally a version whose
 * mappings it copies by member key.
 */
export function NewBridgeVersionDialog({ bridge, defaults, onClose, onCreated }: NewBridgeVersionDialogProps) {
  const create = useCreateBridgeVersion(bridge.bridgeId);
  const form = useForm<NewBridgeVersionValues>({ defaultValues: defaults });

  async function submit(values: NewBridgeVersionValues) {
    const created = await create.mutateAsync({
      ...toBridgeVersionInput(values),
      cloneFromVersionId: values.cloneFromVersionId,
    });
    onCreated(created);
  }

  return (
    <FormDialog
      open
      onOpenChange={open => !open && onClose()}
      title="New version"
      description="A draft that pins a version of each endpoint dimension. A clone copies the source's mappings onto the members with the same keys; mappings whose keys are missing are left out."
      form={form}
      onSubmit={submit}
      submitLabel="Create draft"
    >
      <BridgeVersionFields bridge={bridge} />
      <EntityField
        name="cloneFromVersionId"
        label="Clone from"
        lookupKind="bridge-versions"
        parentId={bridge.bridgeId}
        allowEmpty
        description="Optional: the version whose mappings, factors and weights the draft copies."
      />
    </FormDialog>
  );
}
