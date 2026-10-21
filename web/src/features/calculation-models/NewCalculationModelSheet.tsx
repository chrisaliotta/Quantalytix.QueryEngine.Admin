import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { SwitchField, TextField } from '@/shared/form';
import { useCreateCalculationModel } from './api';
import { ModelLanguageFields } from './ModelLanguageFields';
import type { CalculationModel, NewCalculationModelInput } from './types';

export interface NewCalculationModelSheetProps {
  onClose: () => void;
  onCreated: (model: CalculationModel) => void;
}

/** Creates a calculation model: its key is fixed once created, and it starts without variables or formulas. */
export function NewCalculationModelSheet({ onClose, onCreated }: NewCalculationModelSheetProps) {
  const create = useCreateCalculationModel();
  const form = useForm<NewCalculationModelInput>({
    defaultValues: {
      modelKey: '',
      displayName: '',
      currencyCode: 'USD',
      isEnabled: true,
      formulaLanguageVersion: '1.0',
      attributionMethod: 'shapley',
    },
  });

  async function submit(values: NewCalculationModelInput) {
    onCreated(await create.mutateAsync(values));
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title="New calculation model"
      description="The key is fixed once created. The model starts without variables or formulas, which are live as soon as they are saved."
      form={form}
      onSubmit={submit}
      submitLabel="Create"
    >
      <TextField name="modelKey" label="Key" description="A letter, then letters, digits or underscores." />
      <TextField name="displayName" label="Name" />
      <TextField name="currencyCode" label="Currency" description="The ISO 4217 code of the model's amounts, such as USD." />
      <ModelLanguageFields />
      <SwitchField name="isEnabled" label="Enabled" description="Whether the model is offered for new reports." />
    </EntityFormSheet>
  );
}
