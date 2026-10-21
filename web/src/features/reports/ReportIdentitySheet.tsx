import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { emptyToNull } from '@/shared/text';
import { useUpdateReport } from './api';
import { ReportIdentityFields } from './ReportIdentityFields';
import type { ReportValues } from './reportForm';
import type { Report } from './types';

export interface ReportIdentitySheetProps {
  report: Report;
  onClose: () => void;
}

/** Edits a report's name, type and payload; its key and model are fixed. */
export function ReportIdentitySheet({ report, onClose }: ReportIdentitySheetProps) {
  const update = useUpdateReport(report.reportId);
  const form = useForm<ReportValues>({
    defaultValues: { displayName: report.displayName, reportType: report.reportType, payloadJson: report.payloadJson ?? '' },
  });

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={`Edit ${report.reportKey}`}
      description="An enabled report's change refreshes the query engine's cached report definitions."
      form={form}
      onSubmit={values => update.mutateAsync({ ...values, payloadJson: emptyToNull(values.payloadJson) })}
    >
      <ReportIdentityFields />
    </EntityFormSheet>
  );
}
