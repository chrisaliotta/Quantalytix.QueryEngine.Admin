import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, TextField } from '@/shared/form';
import { emptyToNull } from '@/shared/text';
import type { LookupItem } from '@/shared/types';
import { useCreateReport } from './api';
import { ReportIdentityFields } from './ReportIdentityFields';
import type { ReportValues } from './reportForm';
import type { Report } from './types';

/**
 * Values of the new-report sheet: the identity attributes, the key, the model, and the dimension the rows start from, null for
 * none; a choice not made yet is null.
 */
interface NewReportValues extends ReportValues {
  reportKey: string;
  modelId: number | null;
  startFromDimensionId: number | null;
}

/** The dimension kinds whose members a report's rows can start as a copy of. */
const TEMPLATE_KINDS = ['categorical', 'hierarchical'];

export interface NewReportSheetProps {
  onClose: () => void;
  onCreated: (report: Report) => void;
}

/**
 * Creates a disabled report over a calculation model with a published version, with its own report dimension, keyed and named
 * like it, whose rows are a copy of a starting dimension's published members or a single total row. The key and the model are
 * fixed once created.
 */
export function NewReportSheet({ onClose, onCreated }: NewReportSheetProps) {
  const create = useCreateReport();
  const form = useForm<NewReportValues>({
    defaultValues: { reportKey: '', modelId: null, startFromDimensionId: null, displayName: '', reportType: '', payloadJson: '' },
  });

  async function submit(values: NewReportValues) {
    onCreated(await create.mutateAsync({ ...values, modelId: values.modelId ?? 0, payloadJson: emptyToNull(values.payloadJson) }));
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title="New report"
      description="The key and the model are fixed once created. The report owns its rows, a dimension of its own that its editor edits, and starts disabled until it passes its rules."
      form={form}
      onSubmit={submit}
      submitLabel="Create"
    >
      <TextField name="reportKey" label="Key" description="A letter, then letters, digits or underscores, as report requests name it." />
      <EntityField
        name="modelId"
        label="Calculation model"
        lookupKind="calculation-models"
        filter={(item: LookupItem) => item.classifier != null}
        description="A model with a published version, whose variables and calculated measures the report reads."
      />
      <EntityField
        name="startFromDimensionId"
        label="Start from"
        lookupKind="dimensions"
        allowEmpty
        filter={(item: LookupItem) => TEMPLATE_KINDS.includes(item.classifier ?? '')}
        description="Optional: the rows start as a copy of this dimension's published members, such as another report's rows; without one they start with a single total row."
      />
      <ReportIdentityFields />
    </EntityFormSheet>
  );
}
