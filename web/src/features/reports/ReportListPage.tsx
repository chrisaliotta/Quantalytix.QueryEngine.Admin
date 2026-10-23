import type { ColumnDef } from '@tanstack/react-table';
import { Plus } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { Button } from '@/components/ui/button';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { EntityListPage } from '@/shared/EntityListPage';
import { useLookup } from '@/shared/useLookup';
import { useReports } from './api';
import { NewReportSheet } from './NewReportSheet';
import type { Report } from './types';

/** Lists the tenant's reports and creates new ones. */
export function ReportListPage() {
  const reports = useReports();
  const dimensions = useLookup('dimensions');
  const models = useLookup('calculation-models');
  const navigate = useNavigate();
  const [creating, setCreating] = useState(false);
  const dimensionKeys = new Map(dimensions.data?.map(item => [item.id, item.key]));
  const modelKeys = new Map(models.data?.map(item => [item.id, item.key]));

  const columns: ColumnDef<Report>[] = [
    { accessorKey: 'reportKey', header: 'Key' },
    { accessorKey: 'displayName', header: 'Name' },
    { accessorKey: 'reportType', header: 'Type' },
    {
      id: 'reportDimension',
      header: 'Report dimension',
      accessorFn: report => dimensionKeys.get(report.reportDimensionId) ?? report.reportDimensionId,
    },
    { id: 'model', header: 'Calculation model', accessorFn: report => modelKeys.get(report.calculationModelId) ?? report.calculationModelId },
    {
      accessorKey: 'isEnabled',
      header: 'Enabled',
      cell: ({ row }) => <EnabledBadge enabled={row.original.isEnabled} />,
    },
  ];

  return (
    <>
      <EntityListPage
        title="Reports"
        description="The members of a report dimension as rows and a calculation model's metrics as columns, over each report's own data."
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
