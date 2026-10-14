import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { useUpdateBridgeVersion } from './api';
import { bridgeVersionValues, toBridgeVersionInput, type BridgeVersionValues } from './bridgeVersionForm';
import { BridgeVersionFields } from './BridgeVersionFields';
import type { Bridge, BridgeVersion } from './types';

export interface BridgeVersionSheetProps {
  bridge: Bridge;
  /** The draft version to edit. */
  version: BridgeVersion;
  onClose: () => void;
}

/** Edits a draft version's endpoint versions, cardinality and allocation mode; its mappings stay as they are. */
export function BridgeVersionSheet({ bridge, version, onClose }: BridgeVersionSheetProps) {
  const update = useUpdateBridgeVersion(version.bridgeVersionId);
  const form = useForm<BridgeVersionValues>({ defaultValues: bridgeVersionValues(version) });

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={`Edit v${version.versionNumber}`}
      description="The mappings keep their members: after an endpoint version changes, validation names the mappings whose members are not in it."
      form={form}
      onSubmit={values => update.mutateAsync(toBridgeVersionInput(values))}
    >
      <BridgeVersionFields bridge={bridge} />
    </EntityFormSheet>
  );
}
