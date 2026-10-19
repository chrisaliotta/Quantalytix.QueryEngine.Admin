import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { TextField } from '@/shared/form';
import { emptyToNull } from '@/shared/text';
import { useCreateReport } from './api';
import { ReportIdentityFields } from './ReportIdentityFields';
import type { ReportValues } from './reportForm';
import type { Report } from './types';

/** Values of the new-report sheet: the identity attributes and the key. */
interface NewReportValues extends ReportValues {
  reportKey: string;
}

export interface NewReportSheetProps {
  onClose: () => void;
  onCreated: (report: Report) => void;
}

/** Creates a report: its key is fixed once created, and it starts without versions. */
export function NewReportSheet({ onClose, onCreated }: NewReportSheetProps) {
  const create = useCreateReport();
  const form = useForm<NewReportValues>({
    defaultValues: { reportKey: '', displayName: '', reportType: '', isEnabled: true, payloadJson: '' },
  });

  async function submit(values: NewReportValues) {
    onCreated(await create.mutateAsync({ ...values, payloadJson: emptyToNull(values.payloadJson) }));
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title="New report"
      description="The key is fixed once created. Each version reads one calculation model version, so the report starts without one."
      form={form}
      onSubmit={submit}
      submitLabel="Create"
    >
      <TextField name="reportKey" label="Key" description="A letter, then letters, digits, dots or underscores, as report requests name it." />
      <ReportIdentityFields />
    </EntityFormSheet>
  );
}
