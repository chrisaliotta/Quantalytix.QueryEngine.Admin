import { useForm, useWatch } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { contentSheetTitle } from '@/shared/contentSheet';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { KeyField, NumberField, TextAreaField, TextField, VocabularyField } from '@/shared/form';
import { keyItems } from '@/shared/keyItems';
import { useVocabulary } from '@/shared/useVocabularies';
import { useSaveModelParameter } from './api';
import { allowedValueLines, toParameterInput, toParameterValues, type ParameterValues } from './parameterForm';
import type { ModelParameter } from './types';

export interface ParameterSheetProps {
  modelId: number;
  /** The parameter to edit; null to declare one. */
  parameter: ModelParameter | null;
  onClose: () => void;
}

/**
 * Declares or edits a parameter of the model: its key, which formulas read as `@key`, its type, the texts a text parameter may take,
 * and its default, which every report row takes that neither sets a value nor sits under a row that does. The model's formulas and
 * every report's row values are checked against the change before it is written.
 */
export function ParameterSheet({ modelId, parameter, onClose }: ParameterSheetProps) {
  const save = useSaveModelParameter(modelId);
  const conventions = useVocabulary('day_count_convention');
  const form = useForm<ParameterValues>({ defaultValues: toParameterValues(parameter) });
  const [valueType, allowedValues] = useWatch({ control: form.control, name: ['valueType', 'allowedValues'] });
  const allowed = allowedValueLines(allowedValues);

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={contentSheetTitle('parameter', parameter?.parameterKey ?? null)}
      description="Formulas read parameters as @key on formula language 1.2. Each report sets values on its rows; a row without one takes its nearest ancestor's, and else the default."
      form={form}
      onSubmit={values => save.mutateAsync({ parameterKey: values.parameterKey, body: toParameterInput(values) })}
    >
      <TextField
        name="parameterKey"
        label="Key"
        disabled={parameter !== null}
        description={
          parameter === null
            ? 'A letter, then letters, digits or underscores; never periodDays or yearDays, which every formula reads.'
            : 'Fixed: formulas and report rows name the parameter by it.'
        }
      />
      <TextField name="displayName" label="Name" />
      <VocabularyField
        name="valueType"
        label="Type"
        vocabulary="parameter_value_type"
        description="Formulas read a number wherever they read a value, and compare text with text by =, <> or IN, or pass it to YEARFRAC."
      />
      {valueType === 'number' && <NumberField name="defaultNumber" label="Default" description="The value of every row no row above sets." />}
      {valueType === 'text' && (
        <>
          <TextAreaField
            name="allowedValues"
            label="Allowed values"
            className="max-h-60 overflow-auto font-mono"
            description="One per line; empty for any text. A parameter YEARFRAC reads allows day count conventions only."
          />
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={!conventions.data}
            onClick={() => form.setValue('allowedValues', conventions.data!.values.map(value => value.value).join('\n'))}
          >
            Allow the day count conventions
          </Button>
          {allowed.length > 0 ? (
            <KeyField name="defaultText" label="Default" items={keyItems(allowed)} description="The value of every row no row above sets." />
          ) : (
            <TextField name="defaultText" label="Default" description="The value of every row no row above sets." />
          )}
        </>
      )}
      <TextAreaField name="description" label="Description" />
    </EntityFormSheet>
  );
}
