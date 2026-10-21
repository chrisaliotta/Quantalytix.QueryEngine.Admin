import type { ColumnDef } from '@tanstack/react-table';
import { Plus } from 'lucide-react';
import { useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router';
import { Button } from '@/components/ui/button';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { EntityListPage } from '@/shared/EntityListPage';
import type { DimensionPurpose } from '@/shared/types';
import { useLookup } from '@/shared/useLookup';
import { useBridges } from './api';
import { NewBridgeSheet } from './NewBridgeSheet';
import type { Bridge, BridgeSummary } from './types';

/** The purposes the list narrows to, or every bridge. */
type PurposeFilter = 'all' | DimensionPurpose;

/** A bridge with its endpoint dimensions' keys and its purpose, which the table shows and searches. */
interface BridgeRow extends Bridge {
  sourceDimensionKey: string;
  targetDimensionKey: string;
  purposeText: string;
}

const columns: ColumnDef<BridgeRow>[] = [
  { accessorKey: 'bridgeKey', header: 'Key' },
  { accessorKey: 'displayName', header: 'Name' },
  { accessorKey: 'sourceDimensionKey', header: 'Source dimension' },
  { accessorKey: 'targetDimensionKey', header: 'Target dimension' },
  { accessorKey: 'purposeText', header: 'Purpose' },
  {
    accessorKey: 'isEnabled',
    header: 'Enabled',
    cell: ({ row }) => <EnabledBadge enabled={row.original.isEnabled} />,
  },
];

/** What a bridge is for: analytical, or the report whose source owns it. */
function purposeText(summary: BridgeSummary) {
  return summary.purpose === 'report' ? `Report · ${summary.owner?.reportKey}` : 'Analytical';
}

/**
 * Lists the tenant's bridges with their purpose and creates analytical ones. A report bridge belongs to a source of its report,
 * whose editor creates and edits it; the filter leaves them out when only analytical bridges matter.
 */
export function BridgeListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const filter = (searchParams.get('purpose') ?? 'all') as PurposeFilter;
  const bridges = useBridges(filter === 'all' ? undefined : filter);
  const dimensions = useLookup('dimensions');
  const navigate = useNavigate();
  const [creating, setCreating] = useState(false);
  const dimensionKeys = new Map((dimensions.data ?? []).map(dimension => [dimension.id, dimension.key]));
  const rows = bridges.data?.map(summary => ({
    ...summary.bridge,
    sourceDimensionKey: dimensionKeys.get(summary.bridge.sourceDimensionId) ?? '',
    targetDimensionKey: dimensionKeys.get(summary.bridge.targetDimensionId) ?? '',
    purposeText: purposeText(summary),
  }));

  return (
    <>
      <EntityListPage
        title="Bridges"
        description="Remappings of one dimension's members onto another's, with factors and allocation weights. Report bridges map a report source's items to its rows."
        actions={
          <Button onClick={() => setCreating(true)}>
            <Plus />
            New bridge
          </Button>
        }
        columns={columns}
        data={rows}
        isLoading={bridges.isPending}
        error={bridges.error ?? dimensions.error}
        searchPlaceholder="Search bridges..."
        emptyMessage="No bridges yet."
        onRowClick={bridge => navigate(String(bridge.bridgeId))}
        toolbar={
          <Tabs value={filter} onValueChange={value => setSearchParams(value === 'all' ? {} : { purpose: value }, { replace: true })}>
            <TabsList>
              <TabsTrigger value="all">All</TabsTrigger>
              <TabsTrigger value="analytical">Analytical</TabsTrigger>
              <TabsTrigger value="report">Report</TabsTrigger>
            </TabsList>
          </Tabs>
        }
      />
      {creating && (
        <NewBridgeSheet onClose={() => setCreating(false)} onCreated={bridge => navigate(String(bridge.bridgeId))} />
      )}
    </>
  );
}
