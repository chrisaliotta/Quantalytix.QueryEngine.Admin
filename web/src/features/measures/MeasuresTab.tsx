import { useMutation } from '@tanstack/react-query';
import type { ColumnDef } from '@tanstack/react-table';
import { Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries } from '@/api/queryClient';
import { Button } from '@/components/ui/button';
import { EnabledBadge } from '@/features/datasets/EnabledBadge';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EntityTable } from '@/shared/EntityTable';
import { MeasureSheet } from './MeasureSheet';
import { useMeasures } from './queries';
import type { Measure, MeasureDetail } from './types';

/** A measure as the table shows it: searchable text columns beside the measure itself. */
interface MeasureRow {
  detail: MeasureDetail;
  measureKey: string;
  displayName: string;
  measureKind: string;
  aggregationFunction: string;
  outputType: string;
}

/**
 * Measures tab of the dataset-version editor: the version's measures with their key, name, kind, aggregation function,
 * output type and whether they are enabled. On a draft it creates, edits and deletes them; otherwise the sheet is
 * read-only.
 */
export function MeasuresTab(props: { tenantId: string; datasetVersionId: number; isDraft: boolean }) {
  const { tenantId, datasetVersionId: versionId, isDraft } = props;
  const measures = useMeasures(versionId);
  const [editing, setEditing] = useState<{ detail?: MeasureDetail } | null>(null);
  const [deleting, setDeleting] = useState<MeasureDetail | null>(null);
  const remove = useMutation({
    mutationFn: async (detail: MeasureDetail) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/dataset-versions/{versionId}/measures/{measureId}', {
          params: { path: { tenantId, versionId, measureId: detail.measure.measureId } },
        })
      ),
    onSuccess: (_, detail) => {
      toast.success(`Deleted ${detail.measure.measureKey}.`);
      return invalidateTenantQueries(tenantId);
    },
  });
  const rows = measures.data?.map(toRow);

  const columns: ColumnDef<MeasureRow>[] = [
    { accessorKey: 'measureKey', header: 'Key' },
    { accessorKey: 'displayName', header: 'Name' },
    { accessorKey: 'measureKind', header: 'Kind' },
    { accessorKey: 'aggregationFunction', header: 'Function' },
    { accessorKey: 'outputType', header: 'Output type' },
    { id: 'enabled', header: 'Enabled', cell: ({ row }) => <EnabledBadge enabled={row.original.detail.measure.isEnabled} /> },
    {
      id: 'actions',
      header: '',
      cell: ({ row }) =>
        isDraft && (
          <div className="flex justify-end">
            <Button
              variant="ghost"
              size="icon-sm"
              aria-label={`Delete ${row.original.measureKey}`}
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
    <div data-slot="measures-tab" className="space-y-3">
      <EntityTable
        columns={columns}
        data={rows}
        isLoading={measures.isPending}
        error={measures.error}
        searchPlaceholder="Search measures..."
        emptyMessage="No measures yet."
        onRowClick={row => setEditing({ detail: row.detail })}
        toolbar={
          isDraft && (
            <Button className="ml-auto" onClick={() => setEditing({})}>
              <Plus />
              New measure
            </Button>
          )
        }
      />
      <ApiErrorAlert error={remove.error} />
      {editing && (
        <MeasureSheet versionId={versionId} detail={editing.detail} isDraft={isDraft} onClose={() => setEditing(null)} />
      )}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => {
          if (!open) {
            setDeleting(null);
          }
        }}
        title="Delete this measure?"
        description={`${deleting?.measure.measureKey} will be deleted with its components and time policy. A measure that a formula depends on is kept.`}
        confirmLabel="Delete measure"
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

/** A measure's table row. */
function toRow(detail: MeasureDetail): MeasureRow {
  const measure = detail.measure;
  return {
    detail,
    measureKey: measure.measureKey,
    displayName: measure.displayName,
    measureKind: measure.measureKind,
    aggregationFunction: measure.aggregationFunction ?? '—',
    outputType: outputType(measure),
  };
}

/** A measure's output type, with precision and scale for decimals. */
function outputType(measure: Measure) {
  if (measure.outputDataType === 'decimal') {
    return `decimal(${measure.outputPrecision}, ${measure.outputScale})`;
  }
  return measure.outputDataType;
}
