import { useForm } from 'react-hook-form';
import { EntityField } from '@/shared/form';
import { FormDialog } from '@/shared/FormDialog';
import { useCreateBridgeVersion } from './api';
import { toBridgeVersionInput, type BridgeVersionValues } from './bridgeVersionForm';
import { BridgeVersionFields } from './BridgeVersionFields';
import type { Bridge, BridgeVersion } from './types';

/** Values of the new-version dialog: the version's attributes and the optional clone source. */
export interface NewBridgeVersionValues extends BridgeVersionValues {
  cloneFromVersionId: number | null;
}

export interface NewBridgeVersionDialogProps {
  bridge: Bridge;
  /** The values the fields start with: a clone source's attributes, or defaults for an empty draft. */
  defaults: NewBridgeVersionValues;
  onClose: () => void;
  onCreated: (version: BridgeVersion) => void;
}

/** Creates a draft bridge version: its allocation semantics, and optionally a version whose mappings it copies. */
export function NewBridgeVersionDialog({ bridge, defaults, onClose, onCreated }: NewBridgeVersionDialogProps) {
  const create = useCreateBridgeVersion(bridge.bridgeId);
  const form = useForm<NewBridgeVersionValues>({ defaultValues: defaults });

  async function submit(values: NewBridgeVersionValues) {
    onCreated(await create.mutateAsync({ ...toBridgeVersionInput(values), cloneFromVersionId: values.cloneFromVersionId }));
  }

  return (
    <FormDialog
      open
      onOpenChange={open => !open && onClose()}
      title="New version"
      description="A draft of the bridge. Its mappings name members by key, which requests resolve in the published versions of the two dimensions; publishing the draft replaces the published version."
      form={form}
      onSubmit={submit}
      submitLabel="Create draft"
    >
      <BridgeVersionFields />
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
