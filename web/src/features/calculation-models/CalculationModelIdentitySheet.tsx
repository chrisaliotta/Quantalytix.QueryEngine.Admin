import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { SwitchField, TextField } from '@/shared/form';
import { useUpdateCalculationModel } from './api';
import { ModelLanguageFields } from './ModelLanguageFields';
import type { CalculationModel, CalculationModelInput } from './types';

export interface CalculationModelIdentitySheetProps {
  model: CalculationModel;
  onClose: () => void;
}

/** Edits a calculation model's name, currency, availability, formula language and attribution method; its key is fixed. */
export function CalculationModelIdentitySheet({ model, onClose }: CalculationModelIdentitySheetProps) {
  const update = useUpdateCalculationModel(model.modelId);
  const form = useForm<CalculationModelInput>({
    defaultValues: {
      displayName: model.displayName,
      currencyCode: model.currencyCode,
      isEnabled: model.isEnabled,
      formulaLanguageVersion: model.formulaLanguageVersion,
      attributionMethod: model.attributionMethod,
    },
  });

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={`Edit ${model.modelKey}`}
      description="The enabled reports over the model show a new currency and attribute by a new method at once; neither the name nor the availability changes what queries return."
      form={form}
      onSubmit={values => update.mutateAsync(values)}
    >
      <TextField name="displayName" label="Name" />
      <TextField name="currencyCode" label="Currency" description="The ISO 4217 code of the model's amounts, such as USD." />
      <ModelLanguageFields />
      <SwitchField name="isEnabled" label="Enabled" description="Whether the model is offered for new reports." />
    </EntityFormSheet>
  );
}
