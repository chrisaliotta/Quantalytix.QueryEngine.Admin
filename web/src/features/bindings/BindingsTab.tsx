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
import { useBindings, useGroupingVersion } from './queries';
import type { BindingDetail, DimensionBinding } from './types';

/** A binding as the table shows it: searchable text columns beside the binding itself. */
interface BindingRow {
  detail: BindingDetail;
  displayName: string;
  bindingKind: string;
  dimension: string;
  source: string;
}

/**
 * Bindings tab of the dataset-version editor: the version's dimension bindings with their kind, dimension, source, pinned
 * version, grouping and flags. On a draft it creates, edits and deletes them; otherwise the sheet is read-only.
 */
export function BindingsTab(props: { tenantId: string; datasetVersionId: number; isDraft: boolean }) {
  const { tenantId, datasetVersionId: versionId, isDraft } = props;
  const bindings = useBindings(versionId);
  const dimensions = useLookup('dimensions');
  const fields = useLookup('fields', versionId);
  const [editing, setEditing] = useState<{ detail?: BindingDetail } | null>(null);
  const [deleting, setDeleting] = useState<BindingDetail | null>(null);
  const remove = useMutation({
    mutationFn: async (detail: BindingDetail) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/dataset-versions/{versionId}/bindings/{bindingId}', {
          params: { path: { tenantId, versionId, bindingId: detail.binding.bindingId } },
        })
      ),
    onSuccess: (_, detail) => {
      toast.success(`Deleted ${detail.binding.displayName}.`);
      return invalidateTenantQueries(tenantId);
    },
  });
  const rows = bindings.data && toRows(bindings.data, dimensions.data ?? [], fields.data ?? []);

  const columns: ColumnDef<BindingRow>[] = [
    { accessorKey: 'displayName', header: 'Name' },
    { accessorKey: 'bindingKind', header: 'Kind' },
    { accessorKey: 'dimension', header: 'Dimension' },
    { accessorKey: 'source', header: 'Source' },
    { id: 'pinned', header: 'Pinned version', cell: ({ row }) => <PinnedVersion binding={row.original.detail.binding} /> },
    { id: 'grouping', header: 'Grouping', cell: ({ row }) => <Grouping binding={row.original.detail.binding} /> },
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
              aria-label={`Delete ${row.original.displayName}`}
              onClick={event => {
                event.stopPropagation();
                setDeleting(row.original.detail);
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
        description={`${deleting?.binding.displayName} will be deleted with its time extension. A required binding, or one that other bindings, measures or the version's settings use, is kept.`}
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

/** The table rows: each binding with its dimension key and the field or source binding it reads. */
function toRows(details: BindingDetail[], dimensions: LookupItem[], fields: LookupItem[]): BindingRow[] {
  const dimensionKeys = new Map(dimensions.map(item => [item.id, item.key]));
  const fieldKeys = new Map(fields.map(item => [item.id, item.key]));
  const bindingNames = new Map(details.map(detail => [detail.binding.bindingId, detail.binding.displayName]));
  return details.map(detail => ({
    detail,
    displayName: detail.binding.displayName,
    bindingKind: detail.binding.bindingKind,
    dimension: dimensionKeys.get(detail.binding.dimensionId) ?? String(detail.binding.dimensionId),
    source: sourceText(detail.binding, fieldKeys, bindingNames),
  }));
}

/** The field a binding reads, and for a bridge binding the binding it remaps. */
function sourceText(binding: DimensionBinding, fieldKeys: Map<number, string>, bindingNames: Map<number, string>) {
  const field = nameOf(fieldKeys, binding.sourceFieldId);
  if (binding.bindingKind !== 'bridge') {
    return field ?? '—';
  }
  const via = `via ${nameOf(bindingNames, binding.sourceBindingId)}`;
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

/** The pinned dimension version's key, or a muted "effective" when the binding resolves the version in effect. */
function PinnedVersion({ binding }: { binding: DimensionBinding }) {
  const pinnedId = binding.pinnedDimensionVersionId ?? null;
  const versions = useLookup('dimension-versions', pinnedId === null ? null : binding.dimensionId);
  if (pinnedId === null) {
    return <span className="text-muted-foreground">effective</span>;
  }
  return <>{versions.data?.find(item => item.id === pinnedId)?.key ?? pinnedId}</>;
}

/** The grouping mode with the level, ancestor member or depth it groups at. */
function Grouping({ binding }: { binding: DimensionBinding }) {
  const usesVersion = binding.groupingMode === 'level' || binding.groupingMode === 'ancestor';
  const grouping = useGroupingVersion(usesVersion ? binding.dimensionId : null, binding.pinnedDimensionVersionId ?? null);
  const levels = useLookup('levels', binding.groupingMode === 'level' ? grouping.versionId : null);
  const members = useLookup('members', binding.groupingMode === 'ancestor' ? grouping.versionId : null);
  switch (binding.groupingMode) {
    case 'level':
      return <>level {keyOf(levels.data, binding.groupingLevelId)}</>;
    case 'ancestor':
      return <>under {keyOf(members.data, binding.groupingAncestorMemberId)}</>;
    case 'depth':
      return <>depth {binding.groupingDepth}</>;
    default:
      return <>{binding.groupingMode}</>;
  }
}

/** The key of a level or member, or its ID until the lookup has it. */
function keyOf(items: LookupItem[] | undefined, id: number | null | undefined) {
  return items?.find(item => item.id === id)?.key ?? id;
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
