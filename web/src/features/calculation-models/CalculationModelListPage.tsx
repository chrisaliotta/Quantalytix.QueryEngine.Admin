import type { ColumnDef } from '@tanstack/react-table';
import { Plus } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { Button } from '@/components/ui/button';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { EntityListPage } from '@/shared/EntityListPage';
import { useCalculationModels } from './api';
import { NewCalculationModelSheet } from './NewCalculationModelSheet';
import type { CalculationModel } from './types';

const columns: ColumnDef<CalculationModel>[] = [
  { accessorKey: 'modelKey', header: 'Key' },
  { accessorKey: 'displayName', header: 'Name' },
  {
    accessorKey: 'isEnabled',
    header: 'Enabled',
    cell: ({ row }) => <EnabledBadge enabled={row.original.isEnabled} />,
  },
];

/** Lists the tenant's calculation models and creates new ones. */
export function CalculationModelListPage() {
  const models = useCalculationModels();
  const navigate = useNavigate();
  const [creating, setCreating] = useState(false);

  return (
    <>
      <EntityListPage
        title="Calculation models"
        description="Variables and the formulas over them, which several reports can share; each report brings its own data and dimensions."
        actions={
          <Button onClick={() => setCreating(true)}>
            <Plus />
            New model
          </Button>
        }
        columns={columns}
        data={models.data}
        isLoading={models.isPending}
        error={models.error}
        searchPlaceholder="Search models..."
        emptyMessage="No calculation models yet."
        onRowClick={model => navigate(String(model.calculationModelId))}
      />
      {creating && (
        <NewCalculationModelSheet onClose={() => setCreating(false)} onCreated={model => navigate(String(model.calculationModelId))} />
      )}
    </>
  );
}
