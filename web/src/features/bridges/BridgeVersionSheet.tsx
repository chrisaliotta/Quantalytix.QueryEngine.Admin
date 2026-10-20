import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { useUpdateBridgeVersion } from './api';
import { bridgeVersionValues, toBridgeVersionInput, type BridgeVersionValues } from './bridgeVersionForm';
import { BridgeVersionFields } from './BridgeVersionFields';
import type { BridgeVersion } from './types';

export interface BridgeVersionSheetProps {
  /** The draft or published version to edit. */
  version: BridgeVersion;
  onClose: () => void;
}

/** Edits a version's cardinality and allocation mode; its mappings stay as they are. */
export function BridgeVersionSheet({ version, onClose }: BridgeVersionSheetProps) {
  const update = useUpdateBridgeVersion(version.bridgeVersionId);
  const form = useForm<BridgeVersionValues>({ defaultValues: bridgeVersionValues(version) });

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={`Edit v${version.versionNumber}`}
      description="The mappings stay as they are; validation names the mappings the new semantics do not allow."
      form={form}
      onSubmit={values => update.mutateAsync(toBridgeVersionInput(values))}
    >
      <BridgeVersionFields />
    </EntityFormSheet>
  );
}
