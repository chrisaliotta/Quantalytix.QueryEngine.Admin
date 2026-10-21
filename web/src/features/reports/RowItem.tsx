import { ChevronDown, Eye, GripVertical, Pencil, Plus, Trash2 } from 'lucide-react';
import type { NestableRenderItemProps } from '@registry/components/Nestable';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { ReportRowInput } from './types';

/** The Nestable render props of the row, plus what the row shows and does. */
export interface RowItemProps extends NestableRenderItemProps<ReportRowInput> {
  /** Whether the row can be edited now: not while a save of the tree is pending. */
  isEditable: boolean;
  isSelected: boolean;
  /** How many items the bridge sources map to the row; data rows only. */
  itemCount: number;
  /** Whether the row can be deleted: every row but the report's total. */
  isRemovable: boolean;
  onSelect: () => void;
  onOpen: () => void;
  onAddChild: () => void;
  onDelete: () => void;
}

/**
 * One row of the report's row tree: drag handle, collapse toggle, key, label, kind, what a formula row shows, how the row
 * shows, how many items map to a data row, and its actions. Clicking the row selects it for its source sections.
 */
export function RowItem({
  item,
  depth,
  indentationWidth,
  style,
  collapsed,
  onCollapse,
  setNodeRef,
  setActivatorNodeRef,
  attributes,
  activatorListeners,
  isDragging,
  isEditable,
  isSelected,
  itemCount,
  isRemovable,
  onSelect,
  onOpen,
  onAddChild,
  onDelete,
}: RowItemProps) {
  const row = item.data as ReportRowInput;

  return (
    <div
      ref={setNodeRef}
      data-slot="row-item"
      style={{ ...style, paddingLeft: depth * indentationWidth }}
      className={cn(
        'group flex items-center gap-1 rounded-md px-1 py-1 text-sm hover:bg-accent/50',
        isSelected && 'bg-accent text-accent-foreground',
        isDragging && 'opacity-50'
      )}
    >
      {isEditable && (
        <button
          type="button"
          ref={setActivatorNodeRef}
          aria-label={`Move ${row.key}`}
          className="flex size-6 cursor-grab items-center justify-center rounded-sm text-muted-foreground hover:text-foreground"
          {...attributes}
          {...activatorListeners}
        >
          <GripVertical className="size-4" />
        </button>
      )}
      {onCollapse ? (
        <button
          type="button"
          aria-label={collapsed ? 'Expand' : 'Collapse'}
          className="flex size-6 items-center justify-center rounded-sm text-muted-foreground hover:text-foreground"
          onClick={onCollapse}
        >
          <ChevronDown className={cn('size-4 transition-transform', collapsed && '-rotate-90')} />
        </button>
      ) : (
        <span className="size-6" />
      )}
      <button type="button" className="flex min-w-0 flex-1 items-center gap-2 text-left" onClick={onSelect} onDoubleClick={onOpen}>
        <span className="font-mono text-xs text-muted-foreground">{row.key}</span>
        <span className="truncate">{row.label}</span>
        <Badge variant="outline">{row.kind}</Badge>
        {row.kind === 'formula' && (
          <span className="font-mono text-xs">
            {row.calculatedMeasureKey} → {row.valueColumnKey}
          </span>
        )}
        {row.kind === 'data' && <Badge variant={itemCount === 0 ? 'destructive' : 'secondary'}>{itemCount} items</Badge>}
        {!row.showsValues && <Badge variant="secondary">Header</Badge>}
        {!row.isVisible && <Badge variant="secondary">Hidden</Badge>}
      </button>
      <div className="flex items-center opacity-0 group-hover:opacity-100 focus-within:opacity-100">
        <Button type="button" variant="ghost" size="icon-xs" aria-label={`Open ${row.key}`} onClick={onOpen}>
          {isEditable ? <Pencil /> : <Eye />}
        </Button>
        {isEditable && row.kind !== 'formula' && (
          <Button type="button" variant="ghost" size="icon-xs" aria-label={`Add a row under ${row.key}`} onClick={onAddChild}>
            <Plus />
          </Button>
        )}
        {isEditable && isRemovable && (
          <Button type="button" variant="ghost" size="icon-xs" aria-label={`Delete ${row.key}`} onClick={onDelete}>
            <Trash2 />
          </Button>
        )}
      </div>
    </div>
  );
}
