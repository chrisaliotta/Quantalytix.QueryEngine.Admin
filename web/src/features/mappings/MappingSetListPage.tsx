import type { ColumnDef } from '@tanstack/react-table';
import { Plus } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { Button } from '@/components/ui/button';
import { EntityListPage } from '@/shared/EntityListPage';
import { useLookup } from '@/shared/useLookup';
import { useMappingSets } from './api';
import { NewMappingSetSheet } from './NewMappingSetSheet';
import type { MappingSet } from './types';

/** A mapping set with its dimension's key, which the table shows and searches. */
interface MappingSetRow extends MappingSet {
  dimensionKey: string;
}

const columns: ColumnDef<MappingSetRow>[] = [
  { accessorKey: 'mappingSetKey', header: 'Key' },
  { accessorKey: 'sourceNamespace', header: 'Namespace' },
  { accessorKey: 'dimensionKey', header: 'Dimension' },
  { accessorKey: 'displayName', header: 'Name' },
];

/** Lists the tenant's mapping sets and creates new ones. */
export function MappingSetListPage() {
  const mappingSets = useMappingSets();
  const dimensions = useLookup('dimensions');
  const navigate = useNavigate();
  const [creating, setCreating] = useState(false);
  const dimensionKeys = new Map((dimensions.data ?? []).map(dimension => [dimension.id, dimension.key]));
  const rows = mappingSets.data?.map(mappingSet => ({
    ...mappingSet,
    dimensionKey: dimensionKeys.get(mappingSet.dimensionId) ?? '',
  }));

  return (
    <>
      <EntityListPage
        title="Mappings"
        description="Source code systems and the versions that map their values onto dimension members."
        actions={
          <Button onClick={() => setCreating(true)}>
            <Plus />
            New mapping set
          </Button>
        }
        columns={columns}
        data={rows}
        isLoading={mappingSets.isPending}
        error={mappingSets.error ?? dimensions.error}
        searchPlaceholder="Search mapping sets..."
        emptyMessage="No mapping sets yet."
        onRowClick={mappingSet => navigate(String(mappingSet.mappingSetId))}
      />
      {creating && (
        <NewMappingSetSheet
          onClose={() => setCreating(false)}
          onCreated={mappingSet => navigate(String(mappingSet.mappingSetId))}
        />
      )}
    </>
  );
}
