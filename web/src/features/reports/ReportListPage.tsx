import type { ColumnDef } from '@tanstack/react-table';
import { Plus } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { Button } from '@/components/ui/button';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { EntityListPage } from '@/shared/EntityListPage';
import { useReports } from './api';
import { NewReportSheet } from './NewReportSheet';
import type { Report } from './types';

const columns: ColumnDef<Report>[] = [
  { accessorKey: 'reportKey', header: 'Key' },
  { accessorKey: 'displayName', header: 'Name' },
  { accessorKey: 'reportType', header: 'Type' },
  {
    accessorKey: 'isEnabled',
    header: 'Enabled',
    cell: ({ row }) => <EnabledBadge enabled={row.original.isEnabled} />,
  },
];

/** Lists the tenant's reports and creates new ones. */
export function ReportListPage() {
  const reports = useReports();
  const navigate = useNavigate();
  const [creating, setCreating] = useState(false);

  return (
    <>
      <EntityListPage
        title="Reports"
        description="Line and column layouts over a calculation model; a release pins the version queries run."
        actions={
          <Button onClick={() => setCreating(true)}>
            <Plus />
            New report
          </Button>
        }
        columns={columns}
        data={reports.data}
        isLoading={reports.isPending}
        error={reports.error}
        searchPlaceholder="Search reports..."
        emptyMessage="No reports yet."
        onRowClick={report => navigate(String(report.reportId))}
      />
      {creating && <NewReportSheet onClose={() => setCreating(false)} onCreated={report => navigate(String(report.reportId))} />}
    </>
  );
}
