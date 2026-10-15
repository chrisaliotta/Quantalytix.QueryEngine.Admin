import type { ColumnDef } from '@tanstack/react-table';
import { Plus } from 'lucide-react';
import { useNavigate } from 'react-router';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { EntityListPage } from '@/shared/EntityListPage';
import { StatusBadge } from '@/shared/StatusBadge';
import { useSheetForm } from '@/shared/useSheetForm';
import { useTenantId } from '@/shared/useTenantId';
import { formatDateTime } from '@/shared/utcTime';
import { NewReleaseSheet } from './NewReleaseSheet';
import type { ReleaseCreateInput, ReleaseSummary } from './types';
import { useReleases } from './useReleases';

/** A release summary flattened for the table, so its search and sorting see every column. */
interface ReleaseRow {
  releaseId: number;
  releaseKey: string;
  status: string;
  isActive: boolean;
  datasetCount: number;
  createdAt: string;
  publishedAt: string | null;
}

const COLUMNS: ColumnDef<ReleaseRow>[] = [
  { accessorKey: 'releaseKey', header: 'Key' },
  { accessorKey: 'status', header: 'Status', cell: ({ row }) => <StatusBadge status={row.original.status} /> },
  { accessorKey: 'isActive', header: 'Active', cell: ({ row }) => row.original.isActive && <Badge>Active</Badge> },
  { accessorKey: 'datasetCount', header: 'Datasets' },
  { accessorKey: 'createdAt', header: 'Created', cell: ({ row }) => formatDateTime(row.original.createdAt) },
  { accessorKey: 'publishedAt', header: 'Published', cell: ({ row }) => formatDateTime(row.original.publishedAt) },
];

/** The tenant's releases, newest first; "New release" copies the active release's manifest by default. */
export function ReleasesPage() {
  const tenantId = useTenantId();
  const navigate = useNavigate();
  const releases = useReleases();
  const newRelease = useSheetForm<ReleaseCreateInput>({ releaseKey: '', copyFromReleaseId: null });
  const activeReleaseId = releases.data?.find(summary => summary.isActive)?.release.releaseId ?? null;

  return (
    <>
      <EntityListPage
        title="Releases"
        description="A release pins one dataset version per dataset. Queries use the active release."
        actions={
          <Button onClick={() => newRelease.open({ releaseKey: '', copyFromReleaseId: activeReleaseId })}>
            <Plus />
            New release
          </Button>
        }
        columns={COLUMNS}
        data={releases.data?.map(toRow)}
        isLoading={releases.isPending}
        error={releases.error}
        searchPlaceholder="Search releases..."
        onRowClick={row => navigate(`/t/${tenantId}/releases/${row.releaseId}`)}
      />
      <NewReleaseSheet {...newRelease.sheetProps} />
    </>
  );
}

function toRow(summary: ReleaseSummary): ReleaseRow {
  return {
    releaseId: summary.release.releaseId,
    releaseKey: summary.release.releaseKey,
    status: summary.release.status,
    isActive: summary.isActive,
    datasetCount: summary.datasetCount,
    createdAt: summary.release.createdAt,
    publishedAt: summary.release.publishedAt ?? null,
  };
}
