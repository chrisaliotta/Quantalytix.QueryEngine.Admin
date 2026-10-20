import { useForm, useWatch } from 'react-hook-form';
import { EntityField, JsonField, KeyField, VocabularyField } from '@/shared/form';
import { FormDialog } from '@/shared/FormDialog';
import { emptyToNull } from '@/shared/text';
import { useLookup } from '@/shared/useLookup';
import { useCreateReportVersion } from './api';
import type { Report, ReportVersion } from './types';

/** Values of the new-version dialog; a choice not made yet is null, and JSON is text, empty for none. */
export interface NewReportVersionValues {
  modelId: number | null;
  defaultProfileKey: string | null;
  subtotalPosition: string | null;
  defaultSettingsJson: string;
  /** Carried from a clone source; the version's settings sheet edits them. */
  payloadJson: string;
  defaultFilterJson: string;
  defaultPivotJson: string;
  cloneFromVersionId: number | null;
}

export interface NewReportVersionDialogProps {
  report: Report;
  /** The values the fields start with: a clone source's, or defaults for an empty draft. */
  defaults: NewReportVersionValues;
  onClose: () => void;
  onCreated: (version: ReportVersion) => void;
}

/**
 * Creates a draft report version over a calculation model. Requests read the model version their assumption set pins, by
 * default its published one. A clone copies the source's data, rows and columns, so its profiles are the ones the default
 * profile picker offers; an empty draft names its default profile once it has profiles.
 */
export function NewReportVersionDialog({ report, defaults, onClose, onCreated }: NewReportVersionDialogProps) {
  const create = useCreateReportVersion(report.reportId);
  const form = useForm<NewReportVersionValues>({ defaultValues: defaults });
  const cloneFromVersionId = useWatch({ control: form.control, name: 'cloneFromVersionId' });
  const profiles = useLookup('source-profiles', cloneFromVersionId);

  async function submit(values: NewReportVersionValues) {
    onCreated(
      await create.mutateAsync({
        ...values,
        modelId: values.modelId ?? 0,
        subtotalPosition: values.subtotalPosition ?? '',
        payloadJson: emptyToNull(values.payloadJson),
        defaultFilterJson: emptyToNull(values.defaultFilterJson),
        defaultSettingsJson: emptyToNull(values.defaultSettingsJson),
        defaultPivotJson: emptyToNull(values.defaultPivotJson),
      })
    );
  }

  return (
    <FormDialog
      open
      onOpenChange={open => !open && onClose()}
      title="New version"
      description="A draft that reads a calculation model. Publishing it needs the model to have a published version."
      form={form}
      onSubmit={submit}
      submitLabel="Create draft"
    >
      <EntityField name="modelId" label="Calculation model" lookupKind="calculation-models" />
      <EntityField
        name="cloneFromVersionId"
        label="Clone from"
        lookupKind="report-versions"
        parentId={report.reportId}
        allowEmpty
        description="Optional: the version whose sources, profiles, dimensions, variable scopes, columns and rows the draft copies."
      />
      <KeyField
        name="defaultProfileKey"
        label="Default profile"
        items={cloneFromVersionId === null ? [] : (profiles.data ?? null)}
        allowEmpty
        description="A profile of the clone source a request runs under when it names none; publishing needs an enabled one."
      />
      <VocabularyField
        name="subtotalPosition"
        label="Subtotals"
        vocabulary="subtotal_position"
        description="Whether a member's values come before or after the rows under it."
      />
      <JsonField
        name="defaultSettingsJson"
        label="Default settings"
        description='Such as {"periodicity":"month","timeZone":"UTC","layout":"statement"}. A list layout lets requests filter, order and page the whole table.'
      />
    </FormDialog>
  );
}
