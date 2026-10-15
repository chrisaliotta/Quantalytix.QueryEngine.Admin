import type { ColumnDef } from '@tanstack/react-table';
import { startOfMinute } from 'date-fns';
import { Plus } from 'lucide-react';
import { useNavigate } from 'react-router';
import { Button } from '@/components/ui/button';
import { EntityListPage } from '@/shared/EntityListPage';
import { StatusBadge } from '@/shared/StatusBadge';
import { useSheetForm } from '@/shared/useSheetForm';
import { useTenantId } from '@/shared/useTenantId';
import { formatDateTime } from '@/shared/utcTime';
import { NewAssumptionSetSheet } from './NewAssumptionSetSheet';
import type { AssumptionSet, AssumptionSetCreateInput } from './types';
import { useAssumptionSets } from './useAssumptionSets';

const COLUMNS: ColumnDef<AssumptionSet>[] = [
  { accessorKey: 'assumptionSetKey', header: 'Key' },
  { accessorKey: 'displayName', header: 'Name' },
  { accessorKey: 'effectiveAt', header: 'Effective at', cell: ({ row }) => formatDateTime(row.original.effectiveAt) },
  { accessorKey: 'status', header: 'Status', cell: ({ row }) => <StatusBadge status={row.original.status} /> },
  { accessorKey: 'createdAt', header: 'Created', cell: ({ row }) => formatDateTime(row.original.createdAt) },
  { accessorKey: 'publishedAt', header: 'Published', cell: ({ row }) => formatDateTime(row.original.publishedAt) },
];

/** The tenant's assumption sets, ordered by key, with a sheet that creates one effective now by default. */
export function AssumptionSetsPage() {
  const tenantId = useTenantId();
  const navigate = useNavigate();
  const sets = useAssumptionSets();
  const newSet = useSheetForm<AssumptionSetCreateInput>({ assumptionSetKey: '', displayName: '', description: '', effectiveAt: '' });

  function openNewSet() {
    newSet.open({
      assumptionSetKey: '',
      displayName: '',
      description: '',
      effectiveAt: startOfMinute(new Date()).toISOString(),
    });
  }

  return (
    <>
      <EntityListPage
        title="Assumption sets"
        description="Snapshots that pin exact dimension versions, so reports can be reproduced as of one moment."
        actions={
          <Button onClick={openNewSet}>
            <Plus />
            New assumption set
          </Button>
        }
        columns={COLUMNS}
        data={sets.data}
        isLoading={sets.isPending}
        error={sets.error}
        searchPlaceholder="Search assumption sets..."
        onRowClick={set => navigate(`/t/${tenantId}/assumption-sets/${set.assumptionSetId}`)}
      />
      <NewAssumptionSetSheet {...newSet.sheetProps} />
    </>
  );
}
