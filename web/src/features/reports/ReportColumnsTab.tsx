import { GripVertical, Plus, X } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';
import { Loading } from '@registry/components/Loading';
import { SortableItem, SortableRoot } from '@registry/components/Sortable';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { useReplaceReportColumns, useReportColumns } from './api';
import { toColumnInput } from './columnList';
import { ReportColumnSheet } from './ReportColumnSheet';
import type { ReportColumnInput, ReportVersion } from './types';

export interface ReportColumnsTabProps {
  version: ReportVersion;
  isEditable: boolean;
}

/** The version's columns, once they have loaded. */
export function ReportColumnsTab({ version, isEditable }: ReportColumnsTabProps) {
  const columns = useReportColumns(version.reportVersionId);

  if (columns.isPending) {
    return <Loading size="sm" />;
  }
  if (columns.error) {
    return <ApiErrorAlert error={columns.error} />;
  }
  return <ColumnEditor key={columns.dataUpdatedAt} version={version} isEditable={isEditable} saved={columns.data.map(toColumnInput)} />;
}

/** A column being edited, under an ID that stays the same while its key changes. */
interface ColumnRow {
  id: string;
  column: ReportColumnInput;
}

/** What the column sheet was opened for: an existing row, or a new one. */
interface ColumnSheetRequest {
  row: ColumnRow | null;
}

interface ColumnEditorProps extends ReportColumnsTabProps {
  /** The stored columns, in display order. */
  saved: ReportColumnInput[];
}

/**
 * The version's columns in display order. A draft or the published version reorders them by drag and drop and adds,
 * edits and removes them locally;
 * "Save" replaces the whole list, and a column that keeps its ID keeps the formula rows that place values in it.
 */
function ColumnEditor({ version, isEditable, saved }: ColumnEditorProps) {
  const replace = useReplaceReportColumns(version.reportVersionId);
  const [rows, setRows] = useState<ColumnRow[]>(() => saved.map(column => ({ id: crypto.randomUUID(), column })));
  const [sheet, setSheet] = useState<ColumnSheetRequest | null>(null);
  const isChanged = JSON.stringify(rows.map(row => row.column)) !== JSON.stringify(saved);

  function saveRow(row: ColumnRow | null, column: ReportColumnInput) {
    setRows(current =>
      row === null ? [...current, { id: crypto.randomUUID(), column }] : current.map(other => (other.id === row.id ? { ...other, column } : other))
    );
  }

  function reorder(ids: (string | number)[]) {
    setRows(current => ids.map(id => current.find(row => row.id === id) as ColumnRow));
  }

  return (
    <div data-slot="report-columns-tab" className="space-y-3">
      {isEditable && (
        <div className="flex flex-wrap items-center gap-2">
          <Button size="sm" onClick={() => setSheet({ row: null })}>
            <Plus />
            New column
          </Button>
          <div className="ml-auto flex gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={!isChanged || replace.isPending}
              onClick={() => setRows(saved.map(column => ({ id: crypto.randomUUID(), column })))}
            >
              Discard changes
            </Button>
            <Button
              size="sm"
              disabled={!isChanged || replace.isPending}
              onClick={() => replace.mutate(rows.map(row => row.column), { onSuccess: () => toast.success('Columns saved.') })}
            >
              Save
            </Button>
          </div>
        </div>
      )}
      <ApiErrorAlert error={replace.error} />
      {rows.length === 0 && <p className="py-6 text-center text-sm text-muted-foreground">No columns yet.</p>}
      <SortableRoot items={rows.map(row => row.id)} onItemsChange={reorder} activationConstraint={{ distance: 4 }}>
        <ol data-slot="report-columns" className="space-y-1">
          {rows.map((row, index) => (
            <SortableItem key={row.id} id={row.id} index={index} handle disabled={!isEditable}>
              {({ setNodeRef, setActivatorNodeRef, attributes, activatorListeners, style, isDragging }) => (
                <li
                  ref={setNodeRef}
                  style={style}
                  {...attributes}
                  data-dragging={isDragging}
                  className="flex items-center gap-2 rounded-md border bg-background px-2 py-1.5 text-sm data-[dragging=true]:opacity-70"
                >
                  {isEditable && (
                    <button
                      type="button"
                      ref={setActivatorNodeRef}
                      {...activatorListeners}
                      aria-label={`Drag ${row.column.columnKey}`}
                      className="cursor-grab text-muted-foreground"
                    >
                      <GripVertical className="size-4" />
                    </button>
                  )}
                  <span className="w-5 text-muted-foreground">{index + 1}.</span>
                  <button
                    type="button"
                    className="flex min-w-0 flex-1 items-center gap-2 text-left"
                    onClick={() => setSheet({ row })}
                  >
                    <span className="font-mono text-xs text-muted-foreground">{row.column.columnKey}</span>
                    <span className="truncate">{row.column.displayName}</span>
                    <Badge variant="outline">{row.column.valueKind}</Badge>
                    <span className="font-mono text-xs">{row.column.metricKey}</span>
                    {row.column.displayFormat && <span className="text-xs text-muted-foreground">{row.column.displayFormat}</span>}
                    {!row.column.isVisible && <Badge variant="secondary">Hidden</Badge>}
                  </button>
                  {isEditable && (
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon-xs"
                      aria-label={`Remove ${row.column.columnKey}`}
                      onClick={() => setRows(current => current.filter(other => other.id !== row.id))}
                    >
                      <X />
                    </Button>
                  )}
                </li>
              )}
            </SortableItem>
          ))}
        </ol>
      </SortableRoot>
      {sheet && (
        <ReportColumnSheet
          modelId={version.modelId}
          column={sheet.row?.column ?? null}
          readOnly={!isEditable}
          onSave={column => saveRow(sheet.row, column)}
          onClose={() => setSheet(null)}
        />
      )}
    </div>
  );
}
