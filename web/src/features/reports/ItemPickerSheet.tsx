import { ChevronRight, Folder, FolderCheck, Square, SquareCheck } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Loading } from '@registry/components/Loading';
import { Sheet, SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetTitle } from '@registry/components/Sheet';
import { TreeView } from '@registry/components/TreeView';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { cn } from '@/lib/utils';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { count } from '@/shared/text';
import { useDimensionVersionMembers } from './api';
import { useReplaceSourceItems } from './editorApi';
import { coveringFolder, isFolder, memberTree, otherRows, pickedRules, rulesFrom, toggledRules, type FolderPick } from './itemMappings';
import type { EditorRow, EditorSource, EditorView } from './types';

export interface ItemPickerSheetProps {
  view: EditorView;
  /** The bridge source whose rules are picked. */
  source: EditorSource;
  /** The data row the rules add into. */
  row: EditorRow;
  onClose: () => void;
}

/**
 * Picks a data row's rules in one bridge source from the members of the source's item dimension, in its published version. A
 * folder is taken as one rule, which takes every item under it, including items added later, or as its current items, each taken
 * alone; the items a folder rule takes show as covered. An item may also add into other rows, which it says; the source's other
 * rows keep their rules. The row's rules keep their factors, and new rules start whole.
 */
export function ItemPickerSheet({ view, source, row, onClose }: ItemPickerSheetProps) {
  const replace = useReplaceSourceItems(view.report.reportId);
  const sourceId = source.source.reportSourceId;
  const [rules, setRules] = useState(() => pickedRules(row, sourceId));
  const [pick, setPick] = useState<FolderPick>('folder');
  const members = useDimensionVersionMembers(source.itemDimensionVersionId ?? null);
  const tree = useMemo(() => members.data && memberTree(members.data), [members.data]);
  const folders = [...rules.values()].filter(selection => selection === 'descendants').length;

  return (
    <Sheet open onOpenChange={open => !open && onClose()}>
      <SheetContent className="gap-0 sm:max-w-none" style={{ width: '38rem', maxWidth: '100vw' }}>
        <SheetHeader className="border-b">
          <SheetTitle>
            {source.source.displayName} rules of {row.label}
          </SheetTitle>
          <SheetDescription>
            Pick the members of {source.dimensionRoleKey}'s dimension whose values the row adds up. A folder rule takes every item under the
            folder, including items added to it later.
          </SheetDescription>
        </SheetHeader>
        <div className="flex min-h-0 flex-1 flex-col gap-3 p-4">
          <Tabs value={pick} onValueChange={value => setPick(value as FolderPick)}>
            <TabsList>
              <TabsTrigger value="folder">Pick a folder as one rule</TabsTrigger>
              <TabsTrigger value="items">Pick a folder's current items</TabsTrigger>
            </TabsList>
          </Tabs>
          {!tree && <Loading size="sm" />}
          <ApiErrorAlert error={members.error} />
          {tree && (
            <div className="min-h-0 flex-1 rounded-md border p-2">
              <TreeView
                className="h-full"
                data={tree}
                selectionMode="multiple"
                selectedIds={new Set(rules.keys())}
                onSelectionChange={next => setRules(toggledRules(rules, next, pick, members.data!))}
                defaultOpenIds={new Set(tree.map(node => node.id))}
                allowSearch
                virtualized
                renderItem={({ node, toggle, isExpanded, hasChildren }) => {
                  const key = node.node.data!.memberKey;
                  const covering = coveringFolder(rules, key, members.data!);
                  const others = otherRows(view.rows, row, sourceId, members.data!, key);
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
                      <PickIcon
                        isFolder={isFolder(members.data!, key)}
                        isPicked={rules.has(key)}
                        isCovered={covering !== undefined}
                      />
                      <span className="truncate">{node.label}</span>
                      {covering && <Badge variant="outline">in {covering}</Badge>}
                      {others.length > 0 && <Badge variant="secondary">also on {others.join(', ')}</Badge>}
                    </div>
                  );
                }}
              />
            </div>
          )}
        </div>
        <SheetFooter className="border-t">
          <p className="text-sm text-muted-foreground">
            {count(folders, ['folder', 'folders'])} and {count(rules.size - folders, ['item', 'items'])} picked.
          </p>
          <ApiErrorAlert error={replace.error} />
          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button
              type="button"
              disabled={replace.isPending}
              onClick={() => replace.mutate({ sourceId, rowKey: row.key, items: rulesFrom(row, sourceId, rules) }, { onSuccess: onClose })}
            >
              Save
            </Button>
          </div>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

/** A member's mark in the picker: a folder taken as one rule, an item taken alone, an item a folder rule takes, or neither. */
function PickIcon({ isFolder, isPicked, isCovered }: { isFolder: boolean; isPicked: boolean; isCovered: boolean }) {
  if (isFolder) {
    return isPicked ? <FolderCheck className="size-4 text-primary" /> : <Folder className="size-4 text-muted-foreground" />;
  }
  if (isPicked || isCovered) {
    return <SquareCheck className={cn('size-4', isPicked ? 'text-primary' : 'text-muted-foreground')} />;
  }
  return <Square className="size-4 text-muted-foreground" />;
}
