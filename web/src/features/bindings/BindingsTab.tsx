import { useMutation } from '@tanstack/react-query';
import type { ColumnDef } from '@tanstack/react-table';
import { Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries } from '@/api/queryClient';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EntityTable } from '@/shared/EntityTable';
import type { LookupItem } from '@/shared/types';
import { useLookup } from '@/shared/useLookup';
import { BindingSheet } from './BindingSheet';
import { useBindings } from './queries';
import type { BindingDetail, DimensionBinding } from './types';

/** A binding as the table shows it: searchable text columns beside the binding itself. */
interface BindingRow {
  detail: BindingDetail;
  /** The key requests name the binding by: its role's, or a time binding's field's. */
  key: string;
  role: string;
  bindingKind: string;
  dimension: string;
  source: string;
}

/**
 * Bindings tab of the dataset-version editor: the version's dimension bindings with the key requests name them by, their
 * role, kind, dimension, source and flags. On a draft it creates, edits and deletes them; otherwise the sheet is read-only.
 */
export function BindingsTab(props: { tenantId: string; datasetVersionId: number; isDraft: boolean }) {
  const { tenantId, datasetVersionId: versionId, isDraft } = props;
  const bindings = useBindings(versionId);
  const dimensions = useLookup('dimensions');
  const fields = useLookup('fields', versionId);
  const [editing, setEditing] = useState<{ detail?: BindingDetail } | null>(null);
  const [deleting, setDeleting] = useState<BindingRow | null>(null);
  const remove = useMutation({
    mutationFn: async (row: BindingRow) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/dataset-versions/{versionId}/bindings/{dimensionBindingId}', {
          params: { path: { tenantId, versionId, dimensionBindingId: row.detail.binding.dimensionBindingId } },
        })
      ),
    onSuccess: (_, row) => {
      toast.success(`Deleted ${row.key}.`);
      return invalidateTenantQueries(tenantId);
    },
  });
  const rows = bindings.data && toRows(bindings.data, dimensions.data ?? [], fields.data ?? []);

  const columns: ColumnDef<BindingRow>[] = [
    { accessorKey: 'key', header: 'Key' },
    { accessorKey: 'role', header: 'Role' },
    { accessorKey: 'bindingKind', header: 'Kind' },
    { accessorKey: 'dimension', header: 'Dimension' },
    { accessorKey: 'source', header: 'Source' },
    { id: 'flags', header: 'Flags', cell: ({ row }) => <BindingFlags binding={row.original.detail.binding} /> },
    {
      id: 'actions',
      header: '',
      cell: ({ row }) =>
        isDraft && (
          <div className="flex justify-end">
            <Button
              variant="ghost"
              size="icon-sm"
              aria-label={`Delete ${row.original.key}`}
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
    <div data-slot="bindings-tab" className="space-y-3">
      <EntityTable
        columns={columns}
        data={rows}
        isLoading={bindings.isPending}
        error={bindings.error}
        searchPlaceholder="Search bindings..."
        emptyMessage="No bindings yet. Fields group by their own values until a binding gives them canonical semantics."
        onRowClick={row => setEditing({ detail: row.detail })}
        toolbar={
          isDraft && (
            <Button className="ml-auto" onClick={() => setEditing({})}>
              <Plus />
              New binding
            </Button>
          )
        }
      />
      <ApiErrorAlert error={remove.error} />
      {editing && (
        <BindingSheet versionId={versionId} detail={editing.detail} isDraft={isDraft} onClose={() => setEditing(null)} />
      )}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => {
          if (!open) {
            setDeleting(null);
          }
        }}
        title="Delete this binding?"
        description={`${deleting?.key} will be deleted with its time extension. A required binding, or one that other bindings, measures or the version's settings use, is kept.`}
        confirmLabel="Delete binding"
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

/** The table rows: each binding with its key, role, dimension key and the field or source binding it reads. */
function toRows(details: BindingDetail[], dimensions: LookupItem[], fields: LookupItem[]): BindingRow[] {
  const dimensionKeys = new Map(dimensions.map(item => [item.id, item.key]));
  const fieldKeys = new Map(fields.map(item => [item.id, item.key]));
  const keyOf = (detail: BindingDetail) => detail.role?.dimensionRoleKey ?? nameOf(fieldKeys, detail.binding.sourceFieldId) ?? '—';
  const bindingKeys = new Map(details.map(detail => [detail.binding.dimensionBindingId, keyOf(detail)]));
  return details.map(detail => ({
    detail,
    key: keyOf(detail),
    role: detail.role?.displayName ?? '—',
    bindingKind: detail.binding.bindingKind,
    dimension: dimensionKeys.get(detail.binding.dimensionId) ?? String(detail.binding.dimensionId),
    source: sourceText(detail.binding, fieldKeys, bindingKeys),
  }));
}

/** The field a binding reads, and for a bridge binding the binding it remaps. */
function sourceText(binding: DimensionBinding, fieldKeys: Map<number, string>, bindingKeys: Map<number, string>) {
  const field = nameOf(fieldKeys, binding.sourceFieldId);
  if (binding.bindingKind !== 'bridge') {
    return field ?? '—';
  }
  const via = `via ${nameOf(bindingKeys, binding.sourceDimensionBindingId)}`;
  if (field) {
    return `${field} (${via})`;
  }
  return via;
}

/** The name of an optional reference, or its ID when the lookup does not know it. */
function nameOf(names: Map<number, string>, id: number | null | undefined) {
  if (id === null || id === undefined) {
    return undefined;
  }
  return names.get(id) ?? String(id);
}

/** The binding's flags as badges, plus a muted badge when it is disabled. */
function BindingFlags({ binding }: { binding: DimensionBinding }) {
  return (
    <span className="flex flex-wrap gap-1">
      {binding.isRequired && <Badge variant="outline">required</Badge>}
      {binding.isGroupable && <Badge variant="outline">group</Badge>}
      {binding.isFilterable && <Badge variant="outline">filter</Badge>}
      {!binding.isEnabled && (
        <Badge variant="outline" className="text-muted-foreground">
          disabled
        </Badge>
      )}
    </span>
  );
}
