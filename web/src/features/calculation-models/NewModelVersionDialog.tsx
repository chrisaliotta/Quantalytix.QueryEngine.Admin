import { useForm } from 'react-hook-form';
import { EntityField, TextField } from '@/shared/form';
import { FormDialog } from '@/shared/FormDialog';
import { useCreateModelVersion } from './api';
import type { CalculationModel, CalculationModelVersion, NewModelVersionInput } from './types';

export interface NewModelVersionDialogProps {
  model: CalculationModel;
  /** The values the fields start with: a clone source's currency, or defaults for an empty draft. */
  defaults: NewModelVersionInput;
  onClose: () => void;
  onCreated: (version: CalculationModelVersion) => void;
}

/** Creates a draft model version: its currency, and optionally a version whose metrics it copies. */
export function NewModelVersionDialog({ model, defaults, onClose, onCreated }: NewModelVersionDialogProps) {
  const create = useCreateModelVersion(model.modelId);
  const form = useForm<NewModelVersionInput>({ defaultValues: defaults });

  async function submit(values: NewModelVersionInput) {
    onCreated(await create.mutateAsync(values));
  }

  return (
    <FormDialog
      open
      onOpenChange={open => !open && onClose()}
      title="New version"
      description="A draft of the model. A clone copies the source's variables and calculated measures."
      form={form}
      onSubmit={submit}
      submitLabel="Create draft"
    >
      <TextField name="currencyCode" label="Currency" description="The ISO 4217 code of the model's amounts, such as USD." />
      <EntityField
        name="cloneFromVersionId"
        label="Clone from"
        lookupKind="model-versions"
        parentId={model.modelId}
        allowEmpty
        description="Optional: the version whose metrics the draft copies."
      />
    </FormDialog>
  );
}
