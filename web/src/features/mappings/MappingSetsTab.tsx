import type { ColumnDef } from '@tanstack/react-table';
import { Plus } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { Button } from '@/components/ui/button';
import { mappingSetPath } from '@/features/dimensions/paths';
import { EntityTable } from '@/shared/EntityTable';
import { useTenantId } from '@/shared/useTenantId';
import { useMappingSets } from './api';
import { NewMappingSetSheet } from './NewMappingSetSheet';
import type { MappingSet } from './types';

const columns: ColumnDef<MappingSet>[] = [
  { accessorKey: 'mappingSetKey', header: 'Key' },
  { accessorKey: 'sourceNamespace', header: 'Namespace' },
  { accessorKey: 'displayName', header: 'Name' },
];

/**
 * A dimension's mapping sets, one per source system whose codes map onto its members, created here and each opened on its own
 * page. A set's versions are its own, so the tab shows the same sets whichever dimension version is selected.
 */
export function MappingSetsTab({ dimensionId }: { dimensionId: number }) {
  const tenantId = useTenantId();
  const mappingSets = useMappingSets(dimensionId);
  const navigate = useNavigate();
  const [creating, setCreating] = useState(false);

  function open(mappingSet: MappingSet) {
    navigate(mappingSetPath(tenantId, dimensionId, mappingSet.mappingSetId));
  }

  return (
    <div data-slot="mapping-sets-tab" className="space-y-3">
      <p className="text-sm text-muted-foreground">
        A mapping set maps one source system's codes onto this dimension's members. Its versions are its own, and the assumption
        set pins them apart from the dimension's.
      </p>
      <EntityTable
        columns={columns}
        data={mappingSets.data}
        isLoading={mappingSets.isPending}
        error={mappingSets.error}
        searchPlaceholder="Search mapping sets..."
        emptyMessage="No mapping sets yet."
        onRowClick={open}
        toolbar={
          <Button size="sm" className="ml-auto" onClick={() => setCreating(true)}>
            <Plus />
            New mapping set
          </Button>
        }
      />
      {creating && <NewMappingSetSheet dimensionId={dimensionId} onClose={() => setCreating(false)} onCreated={open} />}
    </div>
  );
}
