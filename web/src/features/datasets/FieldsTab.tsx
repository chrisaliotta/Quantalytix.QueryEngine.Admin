import { useMutation } from '@tanstack/react-query';
import type { ColumnDef } from '@tanstack/react-table';
import { Columns3, DatabaseZap, MoreHorizontal, Plus, TableProperties, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';
import { TruncatedText } from '@registry/components/TruncatedText';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries } from '@/api/queryClient';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EntityTable } from '@/shared/EntityTable';
import { useTenantId } from '@/shared/useTenantId';
import { AddFromColumnsDialog } from './AddFromColumnsDialog';
import { FieldSheet } from './FieldSheet';
import { MaterializeDialog } from './MaterializeDialog';
import { MaterializedColumnDialog } from './MaterializedColumnDialog';
import { useFields } from './queries';
import type { DatasetVersion, Field } from './types';

/** The behavior flags shown in the table, with their short labels. */
const FLAGS = [
  ['isSelectable', 'select'],
  ['isFilterable', 'filter'],
  ['isSortable', 'sort'],
  ['isGroupable', 'group'],
] as const;

/**
 * Fields tab of the dataset-version editor: the version's fields with their sources, types, flags and materialized
 * columns. On a draft it creates fields one by one or from physical columns, and materializes JSON paths.
 */
export function FieldsTab({ version, isDraft }: { version: DatasetVersion; isDraft: boolean }) {
  const tenantId = useTenantId();
  const versionId = version.datasetVersionId;
  const fields = useFields(versionId);
  const [editing, setEditing] = useState<{ field?: Field } | null>(null);
  const [addingFromColumns, setAddingFromColumns] = useState(false);
  const [materializing, setMaterializing] = useState<Field | null>(null);
  const [pointing, setPointing] = useState<Field | null>(null);
  const [deleting, setDeleting] = useState<Field | null>(null);
  const remove = useMutation({
    mutationFn: async (field: Field) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/dataset-versions/{versionId}/fields/{fieldId}', {
          params: { path: { tenantId, versionId, fieldId: field.fieldId } },
        })
      ),
    onSuccess: (_, field) => {
      toast.success(`Deleted ${field.fieldKey}.`);
      return invalidateTenantQueries(tenantId);
    },
  });
  const rows = fields.data ?? [];
  const isTable = version.sourceObjectKind === 'table';

  const columns: ColumnDef<Field>[] = [
    { accessorKey: 'fieldKey', header: 'Key' },
    { accessorKey: 'displayName', header: 'Name' },
    { accessorKey: 'fieldSourceKind', header: 'Source kind' },
    { id: 'source', header: 'Source', cell: ({ row }) => <FieldSource field={row.original} /> },
    { id: 'type', header: 'Type', cell: ({ row }) => fieldType(row.original) },
    { id: 'flags', header: 'Flags', cell: ({ row }) => <FieldFlags field={row.original} /> },
    {
      accessorKey: 'materializedColumn',
      header: 'Materialized column',
      cell: ({ row }) => row.original.materializedColumn ?? '—',
    },
    {
      id: 'actions',
      header: '',
      cell: ({ row }) =>
        isDraft && (
          <FieldActions
            field={row.original}
            canMaterialize={isTable && row.original.fieldSourceKind === 'json_path'}
            onMaterialize={setMaterializing}
            onSetMaterializedColumn={setPointing}
            onDelete={setDeleting}
          />
        ),
    },
  ];

  return (
    <div data-slot="fields-tab" className="space-y-3">
      <EntityTable
        columns={columns}
        data={fields.data}
        isLoading={fields.isPending}
        error={fields.error}
        searchPlaceholder="Search fields..."
        emptyMessage="No fields yet."
        onRowClick={field => setEditing({ field })}
        toolbar={
          isDraft && (
            <div className="ml-auto flex gap-2">
              <Button variant="outline" onClick={() => setAddingFromColumns(true)}>
                <TableProperties />
                Add from columns
              </Button>
              <Button onClick={() => setEditing({})}>
                <Plus />
                New field
              </Button>
            </div>
          )
        }
      />
      <ApiErrorAlert error={remove.error} />
      {editing && (
        <FieldSheet
          version={version}
          field={editing.field}
          fieldKeys={rows.map(field => field.fieldKey)}
          isDraft={isDraft}
          onClose={() => setEditing(null)}
        />
      )}
      <AddFromColumnsDialog
        open={addingFromColumns}
        onOpenChange={setAddingFromColumns}
        version={version}
        fields={rows}
      />
      {materializing && (
        <MaterializeDialog field={materializing} version={version} onClose={() => setMaterializing(null)} />
      )}
      {pointing && <MaterializedColumnDialog field={pointing} version={version} onClose={() => setPointing(null)} />}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => {
          if (!open) {
            setDeleting(null);
          }
        }}
        title="Delete this field?"
        description={`${deleting?.fieldKey} will be deleted. A field that grains, bindings or measures use is kept.`}
        confirmLabel="Delete field"
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

interface FieldActionsProps {
  field: Field;
  /** Whether the field can get a generated column: a json_path field over a table. */
  canMaterialize: boolean;
  onMaterialize: (field: Field) => void;
  onSetMaterializedColumn: (field: Field) => void;
  onDelete: (field: Field) => void;
}

/** Row menu of a draft field; it stops clicks from reaching the row, which opens the field. */
function FieldActions({ field, canMaterialize, onMaterialize, onSetMaterializedColumn, onDelete }: FieldActionsProps) {
  return (
    <div className="flex justify-end" onClick={event => event.stopPropagation()}>
      <DropdownMenu modal={false}>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="icon-sm" aria-label={`Actions for ${field.fieldKey}`}>
            <MoreHorizontal />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          {canMaterialize && (
            <DropdownMenuItem onSelect={() => onMaterialize(field)}>
              <DatabaseZap />
              Materialize…
            </DropdownMenuItem>
          )}
          {field.fieldSourceKind !== 'column' && (
            <DropdownMenuItem onSelect={() => onSetMaterializedColumn(field)}>
              <Columns3 />
              Set materialized column…
            </DropdownMenuItem>
          )}
          <DropdownMenuItem variant="destructive" onSelect={() => onDelete(field)}>
            <Trash2 />
            Delete
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}

/** Where a field's value comes from: its column, its JSON column and path, or its expression. */
function FieldSource({ field }: { field: Field }) {
  if (field.fieldSourceKind === 'expression') {
    return <TruncatedText text={field.expressionText ?? ''} />;
  }
  if (field.fieldSourceKind === 'json_path') {
    return (
      <code className="text-xs">
        {field.sourceColumn} {field.jsonPath}
      </code>
    );
  }
  return <code className="text-xs">{field.sourceColumn}</code>;
}

/** The behavior flags a field has, plus a muted badge when it is disabled. */
function FieldFlags({ field }: { field: Field }) {
  return (
    <span className="flex flex-wrap gap-1">
      {FLAGS.filter(([flag]) => field[flag]).map(([flag, label]) => (
        <Badge key={flag} variant="outline">
          {label}
        </Badge>
      ))}
      {!field.isEnabled && (
        <Badge variant="outline" className="text-muted-foreground">
          disabled
        </Badge>
      )}
    </span>
  );
}

/** A field's data type, with precision and scale for decimals. */
function fieldType(field: Field) {
  if (field.dataType === 'decimal') {
    return `decimal(${field.numericPrecision}, ${field.numericScale})`;
  }
  return field.dataType;
}
