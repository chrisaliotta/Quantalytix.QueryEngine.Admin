import { useMutation } from '@tanstack/react-query';
import type { ColumnDef } from '@tanstack/react-table';
import { Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries } from '@/api/queryClient';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EntityTable } from '@/shared/EntityTable';
import { useLookup } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { GrainSheet } from './GrainSheet';
import { useGrains } from './queries';
import type { Grain } from './types';

/** Grains tab of the dataset-version editor: the row and series grains with their ordered fields. */
export function GrainsTab({ versionId, isDraft }: { versionId: number; isDraft: boolean }) {
  const tenantId = useTenantId();
  const grains = useGrains(versionId);
  const fields = useLookup('fields', versionId);
  const fieldKeys = new Map(fields.data?.map(item => [item.id, item.key]));
  const [editing, setEditing] = useState<{ grain?: Grain } | null>(null);
  const [deleting, setDeleting] = useState<Grain | null>(null);
  const remove = useMutation({
    mutationFn: async (grain: Grain) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/dataset-versions/{versionId}/grains/{grainId}', {
          params: { path: { tenantId, versionId, grainId: grain.grainId } },
        })
      ),
    onSuccess: (_, grain) => {
      toast.success(`Deleted ${grain.grainKey}.`);
      return invalidateTenantQueries(tenantId);
    },
  });

  const columns: ColumnDef<Grain>[] = [
    { accessorKey: 'grainKey', header: 'Key' },
    { accessorKey: 'grainKind', header: 'Kind' },
    {
      id: 'fields',
      header: 'Fields',
      cell: ({ row }) => row.original.fieldIds.map(fieldId => fieldKeys.get(fieldId) ?? fieldId).join(', '),
    },
    {
      id: 'actions',
      header: '',
      cell: ({ row }) =>
        isDraft && (
          <div className="flex justify-end">
            <Button
              variant="ghost"
              size="icon-sm"
              aria-label={`Delete ${row.original.grainKey}`}
              onClick={event => {
                event.stopPropagation();
                setDeleting(row.original);
              }}
            >
              <Trash2 />
            </Button>
          </div>
        ),
    },
  ];

  return (
    <div data-slot="grains-tab" className="space-y-3">
      <EntityTable
        columns={columns}
        data={grains.data}
        isLoading={grains.isPending}
        error={grains.error}
        searchPlaceholder="Search grains..."
        emptyMessage="No grains yet."
        onRowClick={grain => setEditing({ grain })}
        toolbar={
          isDraft && (
            <Button className="ml-auto" onClick={() => setEditing({})}>
              <Plus />
              New grain
            </Button>
          )
        }
      />
      <ApiErrorAlert error={remove.error} />
      {editing && (
        <GrainSheet versionId={versionId} grain={editing.grain} isDraft={isDraft} onClose={() => setEditing(null)} />
      )}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => {
          if (!open) {
            setDeleting(null);
          }
        }}
        title="Delete this grain?"
        description={`${deleting?.grainKey} will be deleted. A grain that a measure's time policy selects by is kept.`}
        confirmLabel="Delete grain"
        destructive
        onConfirm={() => {
          if (deleting) {
            remove.mutate(deleting);
          }
        }}
      />
    </div>
  );
}
