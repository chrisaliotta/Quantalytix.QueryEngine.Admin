import { useForm, useWatch } from 'react-hook-form';
import { EntityField, JsonField } from '@/shared/form';
import { FormDialog } from '@/shared/FormDialog';
import { useCreateReportVersion } from './api';
import { optionalJson } from './reportForm';
import type { CreatedReportVersion, Report } from './types';

/** Values of the new-version dialog; the model only narrows the model versions offered, and is not sent. */
export interface NewReportVersionValues {
  reportDimensionId: number | null;
  modelId: number | null;
  modelVersionId: number | null;
  defaultSourceProfileId: number | null;
  defaultSettingsJson: string | null;
  /** Carried from a clone source; the version's settings sheet edits them. */
  payloadJson: string | null;
  defaultFilterJson: string | null;
  defaultPivotJson: string | null;
  cloneFromVersionId: number | null;
}

export interface NewReportVersionDialogProps {
  report: Report;
  /** The values the fields start with: a clone source's, or defaults for an empty draft. */
  defaults: NewReportVersionValues;
  onClose: () => void;
  onCreated: (created: CreatedReportVersion) => void;
}

/**
 * Creates a draft report version over a report dimension and a model version. A clone copies the source's columns and
 * lines onto the metrics with the same keys in the chosen model version; what has no metric there is left out.
 */
export function NewReportVersionDialog({ report, defaults, onClose, onCreated }: NewReportVersionDialogProps) {
  const create = useCreateReportVersion(report.reportId);
  const form = useForm<NewReportVersionValues>({ defaultValues: defaults });
  const modelId = useWatch({ control: form.control, name: 'modelId' });
  const modelVersionId = useWatch({ control: form.control, name: 'modelVersionId' });

  async function submit({ modelId: _model, ...values }: NewReportVersionValues) {
    const created = await create.mutateAsync({
      ...values,
      reportDimensionId: values.reportDimensionId ?? 0,
      modelVersionId: values.modelVersionId ?? 0,
      payloadJson: optionalJson(values.payloadJson),
      defaultFilterJson: optionalJson(values.defaultFilterJson),
      defaultSettingsJson: optionalJson(values.defaultSettingsJson),
      defaultPivotJson: optionalJson(values.defaultPivotJson),
    });
    onCreated(created);
  }

  return (
    <FormDialog
      open
      onOpenChange={open => !open && onClose()}
      title="New version"
      description="A draft that reads one calculation model version. Publishing it needs that model version published."
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
      <EntityField name="modelVersionId" label="Model version" lookupKind="model-versions" parentId={modelId} />
      <EntityField
        name="defaultSourceProfileId"
        label="Default profile"
        lookupKind="source-profiles"
        parentId={modelVersionId}
        allowEmpty
        description="The profile a request runs under when it names none; publishing needs one that is enabled."
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
