import { ChevronDown, Eye, Pencil, Plus, Trash2 } from 'lucide-react';
import type { NestableRenderItemProps } from '@registry/components/Nestable';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { ReportRowNode } from './rowTree';

/** The Nestable render props of the row, plus what the row does. */
export interface ReportRowItemProps extends NestableRenderItemProps<ReportRowNode> {
  isEditable: boolean;
  /** Opens the overlay row's sheet, or the member's option sheet. */
  onOpen: () => void;
  onAddChild: () => void;
  /** Deletes the overlay row; members belong to the report dimension. */
  onDelete: () => void;
}

/** One row of the report's row tree: collapse toggle, key, name, kind, what a formula row shows, how it shows, and its actions. */
export function ReportRowItem({
  item,
  depth,
  indentationWidth,
  style,
  collapsed,
  onCollapse,
  setNodeRef,
  isEditable,
  onOpen,
  onAddChild,
  onDelete,
}: ReportRowItemProps) {
  const row = item.data as ReportRowNode;
  const isVisible = row.line ? row.line.isVisible : (row.option?.isVisible ?? true);

  return (
    <div
      ref={setNodeRef}
      data-slot="report-row-item"
      style={{ ...style, paddingLeft: depth * indentationWidth }}
      className="group flex items-center gap-1 rounded-md px-1 py-1 text-sm hover:bg-accent/50"
    >
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
      <button type="button" className="flex min-w-0 flex-1 items-center gap-2 text-left" onClick={onOpen}>
        <span className="font-mono text-xs text-muted-foreground">{row.key}</span>
        <span className="truncate">{row.option?.displayName ?? row.name}</span>
        <Badge variant="outline">{row.line?.lineKind ?? 'member'}</Badge>
        {row.line?.lineKind === 'formula' && (
          <span className="font-mono text-xs">
            {row.line.calculatedMeasureKey} → {row.line.valueColumnKey}
          </span>
        )}
        {row.option?.showsValues === false && <Badge variant="secondary">Header</Badge>}
        {!isVisible && <Badge variant="secondary">Hidden</Badge>}
      </button>
      <div className="flex items-center opacity-0 group-hover:opacity-100 focus-within:opacity-100">
        <Button type="button" variant="ghost" size="icon-xs" aria-label={`Open ${row.key}`} onClick={onOpen}>
          {isEditable ? <Pencil /> : <Eye />}
        </Button>
        {isEditable && (
          <>
            <Button type="button" variant="ghost" size="icon-xs" aria-label={`Add a row under ${row.key}`} onClick={onAddChild}>
              <Plus />
            </Button>
            {row.line && (
              <Button type="button" variant="ghost" size="icon-xs" aria-label={`Delete ${row.key}`} onClick={onDelete}>
                <Trash2 />
              </Button>
            )}
          </>
        )}
      </div>
    </div>
  );
}
