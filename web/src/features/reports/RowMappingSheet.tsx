import { ChevronRight, Folder, Square, SquareCheck } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Combobox } from '@registry/components/Combobox';
import { Loading } from '@registry/components/Loading';
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from '@registry/components/Sheet';
import { TreeView } from '@registry/components/TreeView';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { usePublishedVersion } from '@/shared/useLatestVersionItems';
import { useDimensionVersionMembers } from './api';
import { useReplaceRowMappings } from './designApi';
import { itemRows, memberTree, movedItems, rowMappingsFrom, rowSelections, toggledItems } from './designMappings';
import type { DesignRow, ReportDesign } from './types';

export interface RowMappingSheetProps {
  versionId: number;
  design: ReportDesign;
  /** The data row whose items are picked. */
  row: DesignRow;
  onClose: () => void;
}

/**
 * Picks a data row's items, source by source: the members of each source's bound dimension, from its published version, whose
 * values the row adds up. Picking a group picks the members under it that take values. An item on another row says so, and
 * saving moves it here; the row's other items keep their signs, and new ones start positive.
 */
export function RowMappingSheet({ versionId, design, row, onClose }: RowMappingSheetProps) {
  const replace = useReplaceRowMappings(versionId);
  const [bridgeId, setBridgeId] = useState(row.mappings[0]?.bridgeId ?? design.sources[0].bridgeId);
  const [selections, setSelections] = useState(() => rowSelections(row));
  const source = design.sources.find(candidate => candidate.bridgeId === bridgeId)!;
  const dimensionVersion = usePublishedVersion('dimension-versions', source.sourceDimensionId);
  const members = useDimensionVersionMembers(dimensionVersion?.id ?? null);
  const tree = useMemo(() => members.data && memberTree(members.data), [members.data]);
  const selected = selections.get(bridgeId) ?? new Set<string>();
  const placedOn = itemRows(design, bridgeId);
  const moved = movedItems(design, row, selections);
  const count = [...selections.values()].reduce((total, items) => total + items.size, 0);

  function pick(next: Set<string>) {
    setSelections(new Map(selections).set(bridgeId, toggledItems(selected, next, members.data!)));
  }

  return (
    <Sheet open onOpenChange={open => !open && onClose()}>
      <SheetContent className="gap-0 sm:max-w-none" style={{ width: '36rem', maxWidth: '100vw' }}>
        <SheetHeader className="border-b">
          <SheetTitle>Items of {row.label}</SheetTitle>
          <SheetDescription>
            Pick the members of each source's bound dimension whose values the row adds up. Picking a group picks the members
            under it that take values.
          </SheetDescription>
        </SheetHeader>
        <div className="flex min-h-0 flex-1 flex-col gap-3 p-4">
          <Combobox
            size="sm"
            data={design.sources.map(candidate => ({ value: String(candidate.bridgeId), label: `${candidate.label} (${candidate.key})` }))}
            value={String(bridgeId)}
            onValueChange={value => setBridgeId(Number(value))}
            placeholder="Select a source"
            emptyMessage="No sources."
            sort="none"
          />
          {dimensionVersion === null && (
            <p className="text-sm text-muted-foreground">The source's bound dimension has no published version, so it has no items yet.</p>
          )}
          {dimensionVersion !== null && !tree && <Loading size="sm" />}
          <ApiErrorAlert error={members.error} />
          {tree && (
            <div className="min-h-0 flex-1 rounded-md border p-2">
              <TreeView
                key={bridgeId}
                className="h-full"
                data={tree}
                selectionMode="multiple"
                selectedIds={selected}
                onSelectionChange={pick}
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
            {count} items picked{moved.length > 0 && `; ${moved.length} move here from other rows`}.
          </p>
          <ApiErrorAlert error={replace.error} />
          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button
              type="button"
              disabled={replace.isPending}
              onClick={() => replace.mutate({ rowKey: row.key, mappings: rowMappingsFrom(row, selections) }, { onSuccess: onClose })}
            >
              Save
            </Button>
          </div>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}
