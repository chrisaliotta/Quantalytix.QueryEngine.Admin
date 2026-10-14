import { BehaviorSwitch } from '@registry/components/BehaviorSwitch';
import {
  Field,
  FieldContent,
  FieldDescription,
  FieldError,
  FieldTitle,
} from '@/components/ui/field';
import { useFormField, type FormFieldProps } from './useFormField';

/** On/off switch (registry BehaviorSwitch); value is a boolean. The description doubles as its tooltip. */
export function SwitchField({ name, label, description, disabled }: FormFieldProps) {
  const { field, error, isDisabled } = useFormField(name, disabled);
  return (
    <Field orientation="horizontal" data-invalid={error !== undefined}>
      <FieldContent>
        <FieldTitle>{label}</FieldTitle>
        {description && <FieldDescription>{description}</FieldDescription>}
        <FieldError>{error}</FieldError>
      </FieldContent>
      <BehaviorSwitch
        label={label}
        description={description ?? label}
        checked={field.value}
        disabled={isDisabled}
        onCheckedChange={field.onChange}
      />
    </Field>
  );
}
