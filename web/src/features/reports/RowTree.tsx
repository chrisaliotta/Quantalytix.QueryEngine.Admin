import { ChevronDown, Plus } from 'lucide-react';
import { useState } from 'react';
import { Nestable, type NestableItemsChange, type TreeItems } from '@registry/components/Nestable';
import { Button } from '@/components/ui/button';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { useReplaceReportRows } from './editorApi';
import { RowItem } from './RowItem';
import { RowSheet } from './RowSheet';
import { buildRowTree, canPlace, isMemberRow, parentsFor, rowsAfterMove, toRowInput, withoutRow, withRow } from './rowTree';
import type { EditorView, ReportRowInput, ReportRowKind } from './types';

/** The kinds the add menu offers, by name. */
const NEW_ROW_KINDS: { kind: ReportRowKind; label: string }[] = [
  { kind: 'data', label: 'Data row' },
  { kind: 'group', label: 'Group' },
  { kind: 'section', label: 'Section' },
  { kind: 'formula', label: 'Formula row' },
];

/** What the row sheet was opened for: a stored row, or a new row of a kind under a parent (null for the top). */
type RowSheetRequest = { row: ReportRowInput } | { row: null; kind: ReportRowKind; parentKey: string | null };

export interface RowTreeProps {
  view: EditorView;
  /** The row whose source sections show beside the tree. */
  selectedKey: string | null;
  onSelect: (key: string) => void;
}

/**
 * The report's rows as a tree in the engine's order. Rows are dragged to reorder and nest them, and added, edited and deleted in
 * a sheet; every change is saved as a replace of the whole tree, which shows as sent until the API answers. Drops the API would
 * refuse, such as a row under a formula row, are refused while dragging.
 */
export function RowTree({ view, selectedKey, onSelect }: RowTreeProps) {
  const replace = useReplaceReportRows(view.report.reportId);
  const [collapsedKeys, setCollapsedKeys] = useState<ReadonlySet<string>>(() => new Set());
  const [sheet, setSheet] = useState<RowSheetRequest | null>(null);
  const [deleting, setDeleting] = useState<ReportRowInput | null>(null);
  const rows = replace.isPending ? replace.variables : view.rows.map(toRowInput);
  const tree = buildRowTree(rows, collapsedKeys);
  const itemCounts = new Map(view.rows.map(row => [row.key, row.items.length]));

  function handleItemsChange(items: TreeItems<ReportRowInput>, details: NestableItemsChange<ReportRowInput>) {
    if (details.source === 'collapse') {
      setCollapsedKeys(toggled(collapsedKeys, String(details.activeId)));
    }
    if (details.source === 'drag') {
      replace.mutate(rowsAfterMove(rows, items, String(details.activeId)));
    }
  }

  /** Opens a new row of a kind under the selected row when it takes the kind, else under the total or at the top. */
  function openNew(kind: ReportRowKind) {
    const parents = parentsFor(rows, kind, null).map(item => item.key);
    const total = rows.find(row => isMemberRow(row.kind) && row.parentKey == null)?.key ?? null;
    const parentKey = selectedKey !== null && parents.includes(selectedKey) ? selectedKey : isMemberRow(kind) ? total : null;
    setSheet({ row: null, kind, parentKey });
  }

  return (
    <div data-slot="row-tree" className="space-y-3">
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button size="sm">
            <Plus />
            New row
            <ChevronDown />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="start">
          {NEW_ROW_KINDS.map(({ kind, label }) => (
            <DropdownMenuItem key={kind} onSelect={() => openNew(kind)}>
              {label}
            </DropdownMenuItem>
          ))}
        </DropdownMenuContent>
      </DropdownMenu>
      <ApiErrorAlert error={replace.error} />
      <div className="rounded-md border p-2">
        <Nestable
          items={tree}
          onItemsChange={handleItemsChange}
          collapsible
          handle
          enablePointer
          enableTouch={false}
          enableKeyboard
          indentationWidth={24}
          canDrop={({ activeId, projected }) =>
            canPlace(rows, String(activeId), projected.parentId === null ? null : String(projected.parentId))
          }
          renderItem={props => {
            const row = props.item.data as ReportRowInput;
            return (
              <RowItem
                {...props}
                isEditable={!replace.isPending}
                isSelected={row.key === selectedKey}
                itemCount={itemCounts.get(row.key) ?? 0}
                isRemovable={!(isMemberRow(row.kind) && row.parentKey == null)}
                onSelect={() => onSelect(row.key)}
                onOpen={() => setSheet({ row })}
                onAddChild={() => setSheet({ row: null, kind: isMemberRow(row.kind) ? 'data' : 'section', parentKey: row.key })}
                onDelete={() => setDeleting(row)}
              />
            );
          }}
        />
      </div>
      {sheet && (
        <RowSheet
          view={view}
          rows={rows}
          row={sheet.row}
          placement={sheet.row === null ? { kind: sheet.kind, parentKey: sheet.parentKey } : null}
          onSave={row => replace.mutateAsync(withRow(rows, sheet.row?.key ?? null, row))}
          onClose={() => setSheet(null)}
        />
      )}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => !open && setDeleting(null)}
        title={`Delete row ${deleting?.key ?? ''}?`}
        description="Its items become unmapped, its variable scopes are deleted, and the rows under it move up to its parent."
        confirmLabel="Delete row"
        destructive
        onConfirm={() => deleting && replace.mutate(withoutRow(rows, deleting.key))}
      />
    </div>
  );
}

/** The keys with one key added, or removed when it is there. */
function toggled(keys: ReadonlySet<string>, key: string): ReadonlySet<string> {
  const next = new Set(keys);
  if (next.has(key)) {
    next.delete(key);
  } else {
    next.add(key);
  }
  return next;
}
