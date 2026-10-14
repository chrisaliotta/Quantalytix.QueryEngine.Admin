import { ExpressionEditor } from '../ExpressionEditor';
import { FormFieldLayout } from './FormFieldLayout';
import { useFormField, type FormFieldProps } from './useFormField';

export interface ExpressionFieldProps extends FormFieldProps {
  /** Field keys completed after `[f.`. */
  fieldKeys?: string[];
  /** Measure keys completed after `[m.`. */
  measureKeys?: string[];
}

/** Expression editor (Monaco) with `[f.field]` / `[m.measure]` completion; value is the expression text. */
export function ExpressionField({
  name,
  label,
  description,
  disabled,
  fieldKeys,
  measureKeys,
}: ExpressionFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <ExpressionEditor
        value={field.value}
        onChange={field.onChange}
        fieldKeys={fieldKeys}
        measureKeys={measureKeys}
        readOnly={isDisabled}
      />
    </FormFieldLayout>
  );
}
