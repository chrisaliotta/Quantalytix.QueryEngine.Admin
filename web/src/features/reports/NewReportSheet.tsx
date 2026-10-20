import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, TextField } from '@/shared/form';
import { emptyToNull } from '@/shared/text';
import { useCreateReport } from './api';
import { ReportIdentityFields } from './ReportIdentityFields';
import type { ReportValues } from './reportForm';
import type { Report } from './types';

/** Values of the new-report sheet: the identity attributes, the key and the report dimension, which is null until chosen. */
interface NewReportValues extends ReportValues {
  reportKey: string;
  reportDimensionId: number | null;
}

export interface NewReportSheetProps {
  onClose: () => void;
  onCreated: (report: Report) => void;
}

/** Creates a report: its key and report dimension are fixed once created, and it starts without versions. */
export function NewReportSheet({ onClose, onCreated }: NewReportSheetProps) {
  const create = useCreateReport();
  const form = useForm<NewReportValues>({
    defaultValues: { reportKey: '', reportDimensionId: null, displayName: '', reportType: '', isEnabled: true, payloadJson: '' },
  });

  async function submit(values: NewReportValues) {
    onCreated(
      await create.mutateAsync({ ...values, reportDimensionId: values.reportDimensionId ?? 0, payloadJson: emptyToNull(values.payloadJson) })
    );
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title="New report"
      description="The key and report dimension are fixed once created. Each version reads one calculation model, so the report starts without one."
      form={form}
      onSubmit={submit}
      submitLabel="Create"
    >
      <TextField name="reportKey" label="Key" description="A letter, then letters, digits, dots or underscores, as report requests name it." />
      <EntityField
        name="reportDimensionId"
        label="Report dimension"
        lookupKind="dimensions"
        description="The hierarchy whose members are the report's rows, which sources classify into and targets name."
      />
      <ReportIdentityFields />
    </EntityFormSheet>
  );
}
