import type { ColumnDef } from '@tanstack/react-table';
import { Camera } from 'lucide-react';
import { useNavigate } from 'react-router';
import { Button } from '@/components/ui/button';
import { EntityListPage } from '@/shared/EntityListPage';
import { StatusBadge } from '@/shared/StatusBadge';
import { useLookup } from '@/shared/useLookup';
import { useSheetForm } from '@/shared/useSheetForm';
import { useTenantId } from '@/shared/useTenantId';
import { formatDateTime } from '@/shared/utcTime';
import { CaptureSheet } from './CaptureSheet';
import type { AssumptionCaptureInput, AssumptionSet } from './types';
import { useAssumptionSets } from './useAssumptionSets';

/** A set as the list shows it, with the key of the release it reads. */
interface AssumptionSetRow extends AssumptionSet {
  releaseKey: string;
}

const COLUMNS: ColumnDef<AssumptionSetRow>[] = [
  { accessorKey: 'assumptionSetKey', header: 'Key' },
  { accessorKey: 'displayName', header: 'Name' },
  { accessorKey: 'status', header: 'Status', cell: ({ row }) => <StatusBadge status={row.original.status} /> },
  { accessorKey: 'releaseKey', header: 'Release' },
  { accessorKey: 'capturedAt', header: 'Captured', cell: ({ row }) => formatDateTime(row.original.capturedAt) },
];

/**
 * The tenant's assumption sets: the active one, which holds the default versions every publish repoints and reads the active
 * release, then the historical sets captured from it with the release active at the time, which requests name to backtest.
 * "Capture" snapshots the active set.
 */
export function AssumptionSetsPage() {
  const tenantId = useTenantId();
  const navigate = useNavigate();
  const sets = useAssumptionSets();
  const releases = useLookup('releases');
  const capture = useSheetForm<AssumptionCaptureInput>({ assumptionSetKey: '', displayName: '', description: '' });
  const releaseKeys = new Map((releases.data ?? []).map(release => [release.id, release.key]));
  const rows = sets.data?.map(set => ({
    ...set,
    releaseKey: set.releaseId == null ? 'Active release' : (releaseKeys.get(set.releaseId) ?? ''),
  }));

  return (
    <>
      <EntityListPage
        title="Assumption sets"
        description="Requests read the versions the active set pins by default, or the versions and release a historical set captured."
        actions={
          <Button onClick={() => capture.open({ assumptionSetKey: '', displayName: '', description: '' })}>
            <Camera />
            Capture active set
          </Button>
        }
        columns={COLUMNS}
        data={rows}
        isLoading={sets.isPending}
        error={sets.error ?? releases.error}
        searchPlaceholder="Search assumption sets..."
        onRowClick={set => navigate(`/t/${tenantId}/assumption-sets/${set.assumptionSetId}`)}
      />
      <CaptureSheet {...capture.sheetProps} />
    </>
  );
}
