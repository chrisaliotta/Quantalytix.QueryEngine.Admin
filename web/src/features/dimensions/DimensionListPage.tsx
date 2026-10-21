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

/**
 * Lists the tenant's analytical dimensions, which datasets bind and queries group by, and creates new ones. A report dimension
 * holds one report's rows, so its report's editor lists and edits it.
 */
export function DimensionListPage() {
  const dimensions = useDimensions();
  const navigate = useNavigate();
  const [creating, setCreating] = useState(false);

  return (
    <>
      <EntityListPage
        title="Dimensions"
        description="Canonical dimensions that datasets bind and queries group by. Each report's rows are a dimension of its own, edited in the report's editor."
        actions={
          <Button onClick={() => setCreating(true)}>
            <Plus />
            New dimension
          </Button>
        }
        columns={columns}
        data={dimensions.data?.filter(dimension => dimension.dimensionPurpose === 'analytical')}
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
