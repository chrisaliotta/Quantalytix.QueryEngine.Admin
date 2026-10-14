import { JsonEditor } from '../JsonEditor';
import { FormFieldLayout } from './FormFieldLayout';
import { useFormField, type FormFieldProps } from './useFormField';

/** JSON editor (registry JsonEditor); value is the raw JSON text, so parse it in `onSubmit`. */
export function JsonField({ name, label, description, disabled }: FormFieldProps) {
  const { field, id, error, isDisabled } = useFormField(name, disabled);
  return (
    <FormFieldLayout id={id} label={label} description={description} error={error}>
      <div className="h-60">
        <JsonEditor label="" value={field.value} onChange={field.onChange} editable={!isDisabled} />
      </div>
    </FormFieldLayout>
  );
}
