import type { ColumnDef } from '@tanstack/react-table';
import { Plus } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { Button } from '@/components/ui/button';
import { EnabledBadge } from '@/features/datasets/EnabledBadge';
import { EntityListPage } from '@/shared/EntityListPage';
import { useLookup } from '@/shared/useLookup';
import { useBridges } from './api';
import { NewBridgeSheet } from './NewBridgeSheet';
import type { Bridge } from './types';

/** A bridge with its endpoint dimensions' keys, which the table shows and searches. */
interface BridgeRow extends Bridge {
  sourceDimensionKey: string;
  targetDimensionKey: string;
}

const columns: ColumnDef<BridgeRow>[] = [
  { accessorKey: 'bridgeKey', header: 'Key' },
  { accessorKey: 'displayName', header: 'Name' },
  { accessorKey: 'sourceDimensionKey', header: 'Source dimension' },
  { accessorKey: 'targetDimensionKey', header: 'Target dimension' },
  {
    accessorKey: 'isEnabled',
    header: 'Enabled',
    cell: ({ row }) => <EnabledBadge enabled={row.original.isEnabled} />,
  },
];

/** Lists the tenant's bridges and creates new ones. */
export function BridgeListPage() {
  const bridges = useBridges();
  const dimensions = useLookup('dimensions');
  const navigate = useNavigate();
  const [creating, setCreating] = useState(false);
  const dimensionKeys = new Map((dimensions.data ?? []).map(dimension => [dimension.id, dimension.key]));
  const rows = bridges.data?.map(bridge => ({
    ...bridge,
    sourceDimensionKey: dimensionKeys.get(bridge.sourceDimensionId) ?? '',
    targetDimensionKey: dimensionKeys.get(bridge.targetDimensionId) ?? '',
  }));

  return (
    <>
      <EntityListPage
        title="Bridges"
        description="Remappings of one dimension's members onto another's, with factors and allocation weights."
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
      />
      {creating && (
        <NewBridgeSheet onClose={() => setCreating(false)} onCreated={bridge => navigate(String(bridge.bridgeId))} />
      )}
    </>
  );
}
