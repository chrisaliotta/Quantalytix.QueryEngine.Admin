import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { useUpdateReport } from './api';
import { ReportIdentityFields } from './ReportIdentityFields';
import { optionalJson, type ReportValues } from './reportForm';
import type { Report } from './types';

export interface ReportIdentitySheetProps {
  report: Report;
  onClose: () => void;
}

/** Edits a report's name, type, availability and payload; its key is fixed. */
export function ReportIdentitySheet({ report, onClose }: ReportIdentitySheetProps) {
  const update = useUpdateReport(report.reportId);
  const form = useForm<ReportValues>({
    defaultValues: {
      displayName: report.displayName,
      reportType: report.reportType,
      isEnabled: report.isEnabled,
      payloadJson: report.payloadJson ?? null,
    },
  });

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={`Edit ${report.reportKey}`}
      description="A change refreshes the query engine's cached report definitions."
      form={form}
      onSubmit={values => update.mutateAsync({ ...values, payloadJson: optionalJson(values.payloadJson) })}
    >
      <ReportIdentityFields />
    </EntityFormSheet>
  );
}
