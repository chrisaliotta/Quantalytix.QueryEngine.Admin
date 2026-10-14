import { MultiSelect } from '@registry/components/MultiSelect';
import { useVocabulary } from '../useVocabularies';
import { VocabularyOption } from '../VocabularySelect';
import { FormFieldLayout } from './FormFieldLayout';
import { useFormField, type FormFieldProps } from './useFormField';

export interface MultiVocabularyFieldProps extends FormFieldProps {
  /** Vocabulary name, e.g. `'grain_kind'`. */
  vocabulary: string;
}

/** Multi-value vocabulary picker (registry MultiSelect); value is a string array. */
export function MultiVocabularyField({
  name,
  label,
  description,
  disabled,
  vocabulary,
}: MultiVocabularyFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  const { data } = useVocabulary(vocabulary);
  const descriptions = new Map(data?.values.map(option => [option.value, option.description]));

  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <MultiSelect
        options={data?.values.map(option => ({ value: option.value, label: option.value }))}
        value={field.value}
        onChange={field.onChange}
        isLoading={!data}
        disabled={isDisabled}
        sort="none"
        placeholder="Select values"
        renderItemLabel={option => (
          <VocabularyOption value={option.value} description={descriptions.get(option.value)} />
        )}
      />
    </FormFieldLayout>
  );
}
