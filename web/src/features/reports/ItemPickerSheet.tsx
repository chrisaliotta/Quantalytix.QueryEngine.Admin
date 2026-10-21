import { ChevronRight, Folder, Square, SquareCheck } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Loading } from '@registry/components/Loading';
import { Sheet, SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetTitle } from '@registry/components/Sheet';
import { TreeView } from '@registry/components/TreeView';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { useDimensionVersionMembers } from './api';
import { useReplaceSourceItems } from './editorApi';
import { itemRows, itemsFrom, memberTree, movedItems, rowItems, toggledItems } from './itemMappings';
import type { EditorRow, EditorSource, EditorView } from './types';

export interface ItemPickerSheetProps {
  view: EditorView;
  /** The bridge source whose items are picked. */
  source: EditorSource;
  /** The data row the items map to. */
  row: EditorRow;
  onClose: () => void;
}

/**
 * Picks a data row's items in one bridge source: the members of the source's item dimension, from its published version, whose
 * values the row adds up. Picking a group picks the members under it that take values. An item on another row says so, and
 * saving moves it here; the row's other items keep their signs, and new ones start positive.
 */
export function ItemPickerSheet({ view, source, row, onClose }: ItemPickerSheetProps) {
  const replace = useReplaceSourceItems(view.report.reportId);
  const sourceId = source.source.reportSourceId;
  const [selected, setSelected] = useState(() => new Set(rowItems(row, sourceId).map(item => item.itemKey)));
  const members = useDimensionVersionMembers(source.itemDimensionVersionId ?? null);
  const tree = useMemo(() => members.data && memberTree(members.data), [members.data]);
  const placedOn = itemRows(view.rows, sourceId);
  const moved = movedItems(view.rows, row, sourceId, selected);

  return (
    <Sheet open onOpenChange={open => !open && onClose()}>
      <SheetContent className="gap-0 sm:max-w-none" style={{ width: '36rem', maxWidth: '100vw' }}>
        <SheetHeader className="border-b">
          <SheetTitle>
            {source.source.displayName} items of {row.label}
          </SheetTitle>
          <SheetDescription>
            Pick the members of {source.roleKey}'s dimension whose values the row adds up. Picking a group picks the members under it
            that take values.
          </SheetDescription>
        </SheetHeader>
        <div className="flex min-h-0 flex-1 flex-col gap-3 p-4">
          {!tree && <Loading size="sm" />}
          <ApiErrorAlert error={members.error} />
          {tree && (
            <div className="min-h-0 flex-1 rounded-md border p-2">
              <TreeView
                className="h-full"
                data={tree}
                selectionMode="multiple"
                selectedIds={selected}
                onSelectionChange={next => setSelected(toggledItems(selected, next, members.data!))}
                defaultOpenIds={new Set(tree.map(node => node.id))}
                allowSearch
                virtualized
                renderItem={({ node, toggle, isExpanded, isSelected, hasChildren }) => {
                  const item = node.node.data!;
                  const elsewhere = placedOn.get(item.memberKey);
                  return (
                    <div className="flex cursor-pointer items-center gap-2 px-2 py-1 text-sm select-none">
                      <button
                        type="button"
                        aria-label={isExpanded ? 'Collapse' : 'Expand'}
                        className={cn('flex size-4 items-center justify-center', !hasChildren && 'invisible')}
                        onClick={event => {
                          event.stopPropagation();
                          void toggle();
                        }}
                      >
                        <ChevronRight className={cn('size-3.5 text-muted-foreground transition-transform', isExpanded && 'rotate-90')} />
                      </button>
                      {!item.isAssignable ? (
                        <Folder className="size-4 text-muted-foreground" />
                      ) : isSelected ? (
                        <SquareCheck className="size-4 text-primary" />
                      ) : (
                        <Square className="size-4 text-muted-foreground" />
                      )}
                      <span className="truncate">{node.label}</span>
                      {elsewhere && elsewhere.key !== row.key && (
                        <Badge variant={isSelected ? 'default' : 'secondary'}>
                          {isSelected ? `moves from ${elsewhere.label}` : `on ${elsewhere.label}`}
                        </Badge>
                      )}
                    </div>
                  );
                }}
              />
            </div>
          )}
        </div>
        <SheetFooter className="border-t">
          <p className="text-sm text-muted-foreground">
            {selected.size} items picked{moved.length > 0 && `; ${moved.length} move here from other rows`}.
          </p>
          <ApiErrorAlert error={replace.error} />
          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button
              type="button"
              disabled={replace.isPending}
              onClick={() => replace.mutate({ sourceId, rowKey: row.key, items: itemsFrom(row, sourceId, selected) }, { onSuccess: onClose })}
            >
              Save
            </Button>
          </div>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}
