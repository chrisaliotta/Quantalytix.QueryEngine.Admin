import type { ColumnDef } from '@tanstack/react-table';
import { Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EntityTable } from '@/shared/EntityTable';
import { useDeleteRole, useRoles } from './api';
import { RoleSheet } from './RoleSheet';
import type { DimensionRole } from './types';

const baseColumns: ColumnDef<DimensionRole>[] = [
  { accessorKey: 'roleKey', header: 'Key' },
  { accessorKey: 'displayName', header: 'Name' },
  { accessorKey: 'description', header: 'Description', cell: ({ row }) => row.original.description ?? '—' },
];

/** What the role sheet was opened for: an existing role, or a new one. */
interface RoleSheetRequest {
  role: DimensionRole | null;
}

/**
 * The dimension's roles in a table, added, edited and deleted in a sheet. Roles are not versioned, so the tab shows the same
 * roles whichever version is selected.
 */
export function RolesTab({ dimensionId }: { dimensionId: number }) {
  const roles = useRoles(dimensionId);
  const deleteRole = useDeleteRole(dimensionId);
  const [sheet, setSheet] = useState<RoleSheetRequest | null>(null);
  const [deleting, setDeleting] = useState<DimensionRole | null>(null);

  const columns: ColumnDef<DimensionRole>[] = [
    ...baseColumns,
    {
      id: 'actions',
      header: '',
      cell: ({ row }) => (
        <Button
          variant="ghost"
          size="icon-xs"
          aria-label={`Delete ${row.original.roleKey}`}
          onClick={event => {
            event.stopPropagation();
            setDeleting(row.original);
          }}
        >
          <Trash2 />
        </Button>
      ),
    },
  ];

  return (
    <div data-slot="roles-tab" className="space-y-3">
      <p className="text-sm text-muted-foreground">
        Roles apply to every version of the dimension. A binding binds a field to a role, and requests name the binding by
        the role's key.
      </p>
      <EntityTable
        columns={columns}
        data={roles.data}
        isLoading={roles.isPending}
        error={roles.error}
        emptyMessage="No roles yet; bindings need one."
        onRowClick={role => setSheet({ role })}
        toolbar={
          <Button size="sm" className="ml-auto" onClick={() => setSheet({ role: null })}>
            <Plus />
            New role
          </Button>
        }
      />
      <ApiErrorAlert error={deleteRole.error} />
      {sheet && <RoleSheet dimensionId={dimensionId} role={sheet.role} onClose={() => setSheet(null)} />}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => !open && setDeleting(null)}
        title={`Delete role ${deleting?.roleKey ?? ''}?`}
        description="A role that bindings, calculation dimensions or variable sources name cannot be deleted."
        confirmLabel="Delete role"
        destructive
        onConfirm={() => deleting && deleteRole.mutate(deleting.roleId)}
      />
    </div>
  );
}
