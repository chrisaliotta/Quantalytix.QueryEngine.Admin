import type { ColumnDef } from '@tanstack/react-table';
import { Plus } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { Button } from '@/components/ui/button';
import { EntityListPage } from '@/shared/EntityListPage';
import { EnabledBadge } from './EnabledBadge';
import { NewDatasetSheet } from './NewDatasetSheet';
import { useDatasets } from './queries';
import type { Dataset } from './types';

const columns: ColumnDef<Dataset>[] = [
  { accessorKey: 'datasetKey', header: 'Key' },
  { accessorKey: 'displayName', header: 'Display name' },
  {
    accessorKey: 'isEnabled',
    header: 'Enabled',
    cell: ({ row }) => <EnabledBadge enabled={row.original.isEnabled} />,
  },
];

/** The datasets of the tenant; a row opens the dataset, and "New dataset" creates one. */
export function DatasetListPage() {
  const datasets = useDatasets();
  const navigate = useNavigate();
  const [creating, setCreating] = useState(false);

  return (
    <>
      <EntityListPage
        title="Datasets"
        description="Logical datasets that query requests name, each with versioned fields, grains, bindings and measures."
        actions={
          <Button onClick={() => setCreating(true)}>
            <Plus />
            New dataset
          </Button>
        }
        columns={columns}
        data={datasets.data}
        isLoading={datasets.isPending}
        error={datasets.error}
        searchPlaceholder="Search datasets..."
        emptyMessage="No datasets yet."
        onRowClick={dataset => navigate(String(dataset.datasetId))}
      />
      <NewDatasetSheet
        open={creating}
        onOpenChange={setCreating}
        onCreated={dataset => navigate(String(dataset.datasetId))}
      />
    </>
  );
}
