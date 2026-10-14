import { useState } from 'react';
import { useForm, type DefaultValues, type FieldValues } from 'react-hook-form';

/**
 * A sheet's form and open state for `EntityFormSheet`: `open(values)` resets the form to the
 * values, then opens the sheet, so every opening starts from the entity (or the defaults) it is for.
 * @example const sheet = useSheetForm<ReleaseCreateInput>(defaults); sheet.open(values); <NewReleaseSheet {...sheet.sheetProps} />
 */
export function useSheetForm<TValues extends FieldValues>(defaultValues: DefaultValues<TValues>) {
  const form = useForm<TValues>({ defaultValues });
  const [isOpen, setIsOpen] = useState(false);

  function open(values: TValues) {
    form.reset(values);
    setIsOpen(true);
  }

  return { open, sheetProps: { form, open: isOpen, onOpenChange: setIsOpen } };
}
