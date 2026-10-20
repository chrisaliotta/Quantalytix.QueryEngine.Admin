import { useForm, useWatch } from 'react-hook-form';
import { EntityField, JsonField, KeyField } from '@/shared/form';
import { FormDialog } from '@/shared/FormDialog';
import { emptyToNull } from '@/shared/text';
import { usePublishedVersionItems } from '@/shared/useLatestVersionItems';
import { useCreateReportVersion } from './api';
import type { Report, ReportVersion } from './types';

/** Values of the new-version dialog; JSON is text, empty for none. */
export interface NewReportVersionValues {
  reportDimensionId: number | null;
  modelId: number | null;
  defaultProfileKey: string | null;
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
 * Creates a draft report version over a report dimension and a calculation model. Requests read the model version their
 * assumption set pins, by default its published one, whose profiles the default profile picker offers. A clone copies the
 * source's columns and lines.
 */
export function NewReportVersionDialog({ report, defaults, onClose, onCreated }: NewReportVersionDialogProps) {
  const create = useCreateReportVersion(report.reportId);
  const form = useForm<NewReportVersionValues>({ defaultValues: defaults });
  const modelId = useWatch({ control: form.control, name: 'modelId' });
  const profiles = usePublishedVersionItems('model-versions', modelId, 'source-profiles');

  async function submit(values: NewReportVersionValues) {
    onCreated(
      await create.mutateAsync({
        ...values,
        reportDimensionId: values.reportDimensionId ?? 0,
        modelId: values.modelId ?? 0,
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
      <EntityField
        name="reportDimensionId"
        label="Report dimension"
        lookupKind="dimensions"
        description="The hierarchy of report items that member lines and variable targets name."
      />
      <EntityField name="modelId" label="Calculation model" lookupKind="calculation-models" />
      <KeyField
        name="defaultProfileKey"
        label="Default profile"
        items={profiles}
        allowEmpty
        description="The profile a request runs under when it names none; publishing needs one the model enables."
      />
      <JsonField name="defaultSettingsJson" label="Default settings" description='Such as {"periodicity":"month","timeZone":"UTC"}.' />
      <EntityField
        name="cloneFromVersionId"
        label="Clone from"
        lookupKind="report-versions"
        parentId={report.reportId}
        allowEmpty
        description="Optional: the version whose columns and lines the draft copies."
      />
    </FormDialog>
  );
}
