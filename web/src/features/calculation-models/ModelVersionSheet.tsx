import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { TextField } from '@/shared/form';
import { useUpdateModelVersion } from './api';
import type { CalculationModelVersion, ModelVersionInput } from './types';

export interface ModelVersionSheetProps {
  /** The draft or published version to edit. */
  version: CalculationModelVersion;
  onClose: () => void;
}

/** Edits a draft or published model version's currency; its content stays as it is. */
export function ModelVersionSheet({ version, onClose }: ModelVersionSheetProps) {
  const update = useUpdateModelVersion(version.modelVersionId);
  const form = useForm<ModelVersionInput>({ defaultValues: { currencyCode: version.currencyCode } });

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={`Edit v${version.versionNumber}`}
      form={form}
      onSubmit={values => update.mutateAsync(values)}
    >
      <TextField name="currencyCode" label="Currency" description="The ISO 4217 code of the model's amounts, such as USD." />
    </EntityFormSheet>
  );
}
