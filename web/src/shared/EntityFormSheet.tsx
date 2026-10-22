import type { ReactNode } from 'react';
import { FormProvider, type FieldValues, type UseFormReturn } from 'react-hook-form';
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from '@registry/components/Sheet';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from './ApiErrorAlert';
import { FormReadOnlyContext } from './form/FormReadOnlyContext';
import { useProblemSubmit } from './form/useProblemSubmit';

export interface EntityFormSheetProps<TValues extends FieldValues> {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
  /** The page's `useForm` instance; `form.reset(values)` before opening it for an entity. */
  form: UseFormReturn<TValues>;
  /** Saves the values, e.g. `mutation.mutateAsync`. An `ApiProblem` with `errors` is mapped onto the fields. */
  onSubmit: (values: TValues) => Promise<unknown>;
  submitLabel?: string;
  /** Disables every field and hides the submit button, e.g. for published versions. */
  readOnly?: boolean;
  /** CSS width of the sheet. */
  width?: string;
  /** Form fields from `@/shared/form` (or any control using `useFormContext`). */
  children: ReactNode;
}

/**
 * Side sheet hosting an entity form: provides the form context, submits, shows API validation
 * errors on their fields (plus a summary) and other problems in an alert, and closes on success.
 */
export function EntityFormSheet<TValues extends FieldValues>({
  open,
  onOpenChange,
  title,
  description,
  width = '32rem',
  ...formProps
}: EntityFormSheetProps<TValues>) {
  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="gap-0 sm:max-w-none" style={{ width, maxWidth: '100vw' }}>
        <SheetHeader className="border-b">
          <SheetTitle>{title}</SheetTitle>
          {description && <SheetDescription>{description}</SheetDescription>}
        </SheetHeader>
        <EntityForm onOpenChange={onOpenChange} {...formProps} />
      </SheetContent>
    </Sheet>
  );
}

type EntityFormProps<TValues extends FieldValues> = Pick<
  EntityFormSheetProps<TValues>,
  'onOpenChange' | 'form' | 'onSubmit' | 'submitLabel' | 'readOnly' | 'children'
>;

/** Mounted only while the sheet is open, so each opening starts without a stale problem. */
function EntityForm<TValues extends FieldValues>({
  onOpenChange,
  form,
  onSubmit,
  submitLabel = 'Save',
  readOnly = false,
  children,
}: EntityFormProps<TValues>) {
  const { submit, problem } = useProblemSubmit(form, onSubmit, () => onOpenChange(false));
  const { isSubmitting } = form.formState;

  return (
    <FormProvider {...form}>
      <form className="flex min-h-0 flex-1 flex-col" onSubmit={submit}>
        <FormReadOnlyContext value={readOnly}>
          <fieldset
            data-slot="entity-form-fields"
            disabled={readOnly || isSubmitting}
            className="min-h-0 min-w-0 flex-1 space-y-5 overflow-y-auto p-4"
          >
            {children}
          </fieldset>
        </FormReadOnlyContext>
        <SheetFooter className="border-t">
          <ApiErrorAlert error={problem} />
          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              {readOnly ? 'Close' : 'Cancel'}
            </Button>
            {!readOnly && (
              <Button type="submit" disabled={isSubmitting}>
                {submitLabel}
              </Button>
            )}
          </div>
        </SheetFooter>
      </form>
    </FormProvider>
  );
}
