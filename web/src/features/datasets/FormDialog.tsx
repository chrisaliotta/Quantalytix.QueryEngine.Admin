import type { ReactNode } from 'react';
import { FormProvider, type FieldValues, type UseFormReturn } from 'react-hook-form';
import { Loading } from '@registry/components/Loading';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { useProblemSubmit } from './useProblemSubmit';

export interface FormDialogProps<TValues extends FieldValues> {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: ReactNode;
  form: UseFormReturn<TValues>;
  /** Saves the values, e.g. `mutation.mutateAsync`. An `ApiProblem` with `errors` is mapped onto the fields. */
  onSubmit: (values: TValues) => Promise<unknown>;
  submitLabel: string;
  /** Shown with a spinner while the submit runs, for slow requests such as a schema change. */
  pendingMessage?: string;
  /** Form fields from `@/shared/form` or this feature. */
  children: ReactNode;
}

/**
 * Dialog hosting a small form, the dialog counterpart of `EntityFormSheet`: API validation errors show on their
 * fields, other problems in an alert, and the dialog closes on success.
 */
export function FormDialog<TValues extends FieldValues>({
  open,
  onOpenChange,
  title,
  description,
  ...formProps
}: FormDialogProps<TValues>) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent data-slot="form-dialog">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          {description && <DialogDescription>{description}</DialogDescription>}
        </DialogHeader>
        <DialogForm onOpenChange={onOpenChange} {...formProps} />
      </DialogContent>
    </Dialog>
  );
}

type DialogFormProps<TValues extends FieldValues> = Pick<
  FormDialogProps<TValues>,
  'onOpenChange' | 'form' | 'onSubmit' | 'submitLabel' | 'pendingMessage' | 'children'
>;

/** Mounted only while the dialog is open, so each opening starts without a stale problem. */
function DialogForm<TValues extends FieldValues>({
  onOpenChange,
  form,
  onSubmit,
  submitLabel,
  pendingMessage,
  children,
}: DialogFormProps<TValues>) {
  const { submit, problem } = useProblemSubmit(form, onSubmit, () => onOpenChange(false));
  const { isSubmitting } = form.formState;

  return (
    <FormProvider {...form}>
      <form className="min-w-0 space-y-4" onSubmit={submit}>
        <fieldset disabled={isSubmitting} className="min-w-0 space-y-4">
          {children}
        </fieldset>
        {isSubmitting && pendingMessage && <Loading size="sm" description={pendingMessage} />}
        <ApiErrorAlert error={problem} />
        <DialogFooter>
          <Button type="button" variant="outline" disabled={isSubmitting} onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button type="submit" disabled={isSubmitting}>
            {submitLabel}
          </Button>
        </DialogFooter>
      </form>
    </FormProvider>
  );
}
