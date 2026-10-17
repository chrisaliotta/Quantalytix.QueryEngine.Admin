import { useReport } from './api';

/** Breadcrumb label of a report: its key once loaded. */
export function ReportCrumb({ reportId }: { reportId: number }) {
  const report = useReport(reportId);
  return <>{report.data?.reportKey ?? 'Report'}</>;
}
