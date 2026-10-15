import type { ColumnDef } from '@tanstack/react-table';
import { Plus } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { Button } from '@/components/ui/button';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { EntityListPage } from '@/shared/EntityListPage';
import { useDimensions } from './api';
import { NewDimensionSheet } from './NewDimensionSheet';
import type { Dimension } from './types';

const columns: ColumnDef<Dimension>[] = [
  { accessorKey: 'dimensionKey', header: 'Key' },
  { accessorKey: 'displayName', header: 'Name' },
  { accessorKey: 'dimensionKind', header: 'Kind' },
  { accessorKey: 'definitionOrigin', header: 'Origin' },
  {
    accessorKey: 'isEnabled',
    header: 'Enabled',
    cell: ({ row }) => <EnabledBadge enabled={row.original.isEnabled} />,
  },
];

/** Lists the tenant's dimensions and creates new ones. */
export function DimensionListPage() {
  const dimensions = useDimensions();
  const navigate = useNavigate();
  const [creating, setCreating] = useState(false);

  return (
    <>
      <EntityListPage
        title="Dimensions"
        description="Canonical dimensions and their effective-dated versions."
        actions={
          <Button onClick={() => setCreating(true)}>
            <Plus />
            New dimension
          </Button>
        }
        columns={columns}
        data={dimensions.data}
        isLoading={dimensions.isPending}
        error={dimensions.error}
        searchPlaceholder="Search dimensions..."
        onRowClick={dimension => navigate(String(dimension.dimensionId))}
      />
      {creating && (
        <NewDimensionSheet
          onClose={() => setCreating(false)}
          onCreated={dimension => navigate(String(dimension.dimensionId))}
        />
      )}
    </>
  );
}
