import { VocabularySelect } from '../VocabularySelect';
import { FormFieldLayout } from './FormFieldLayout';
import { useFormField, type FormFieldProps } from './useFormField';

export interface VocabularyFieldProps extends FormFieldProps {
  /** Vocabulary name, e.g. `'data_type'`. */
  vocabulary: string;
  /** Lets the user clear the value back to null. */
  allowEmpty?: boolean;
}

/** Vocabulary picker (VocabularySelect); value is the selected value, or null. */
export function VocabularyField({
  name,
  label,
  description,
  disabled,
  vocabulary,
  allowEmpty,
}: VocabularyFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <VocabularySelect
        vocabulary={vocabulary}
        value={field.value}
        onChange={field.onChange}
        allowEmpty={allowEmpty}
        disabled={isDisabled}
      />
    </FormFieldLayout>
  );
}
