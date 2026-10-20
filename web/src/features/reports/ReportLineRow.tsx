import { ChevronDown, Eye, GripVertical, Pencil, Plus, Trash2 } from 'lucide-react';
import type { NestableRenderItemProps } from '@registry/components/Nestable';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { ReportLineInput } from './types';

/** The Nestable render props of the row, plus what the row does. */
export interface ReportLineRowProps extends NestableRenderItemProps<ReportLineInput> {
  isEditable: boolean;
  canDrag: boolean;
  onOpen: () => void;
  onAddChild: () => void;
  onDelete: () => void;
}

/** One line of the Nestable tree: collapse toggle, drag handle (editable versions only), key, name, what it shows, and its actions. */
export function ReportLineRow({
  item,
  depth,
  indentationWidth,
  style,
  isDragging,
  collapsed,
  onCollapse,
  setNodeRef,
  setActivatorNodeRef,
  attributes,
  activatorListeners,
  isEditable,
  canDrag,
  onOpen,
  onAddChild,
  onDelete,
}: ReportLineRowProps) {
  const line = item.data as ReportLineInput;

  return (
    <div
      ref={setNodeRef}
      data-slot="report-line-row"
      style={{ ...style, paddingLeft: depth * indentationWidth }}
      className={cn('group flex items-center gap-1 rounded-md px-1 py-1 text-sm hover:bg-accent/50', isDragging && 'opacity-60')}
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
      {canDrag && (
        <button
          type="button"
          ref={setActivatorNodeRef}
          aria-label={`Drag ${line.lineKey}`}
          className="flex size-6 cursor-grab items-center justify-center rounded-sm text-muted-foreground hover:text-foreground"
          {...attributes}
          {...activatorListeners}
        >
          <GripVertical className="size-4" />
        </button>
      )}
      <button type="button" className="flex min-w-0 flex-1 items-center gap-2 text-left" onClick={onOpen}>
        <span className="font-mono text-xs text-muted-foreground">{line.lineKey}</span>
        <span className="truncate">{line.displayName}</span>
        <Badge variant="outline">{line.lineKind}</Badge>
        <LineTarget line={line} />
        {!line.isVisible && <Badge variant="secondary">Hidden</Badge>}
      </button>
      <div className="flex items-center opacity-0 group-hover:opacity-100 focus-within:opacity-100">
        <Button type="button" variant="ghost" size="icon-xs" aria-label={`Open ${line.lineKey}`} onClick={onOpen}>
          {isEditable ? <Pencil /> : <Eye />}
        </Button>
        {isEditable && (
          <>
            <Button type="button" variant="ghost" size="icon-xs" aria-label={`Add a line under ${line.lineKey}`} onClick={onAddChild}>
              <Plus />
            </Button>
            <Button type="button" variant="ghost" size="icon-xs" aria-label={`Delete ${line.lineKey}`} onClick={onDelete}>
              <Trash2 />
            </Button>
          </>
        )}
      </div>
    </div>
  );
}

/** What a line shows: the member of a member line, or the measure and column of a formula line. */
function LineTarget({ line }: { line: ReportLineInput }) {
  if (line.lineKind === 'member') {
    return <span className="font-mono text-xs">{line.reportMemberKey}</span>;
  }
  if (line.lineKind === 'formula') {
    return (
      <span className="font-mono text-xs">
        {line.calculatedMeasureKey} → {line.valueColumnKey}
      </span>
    );
  }
  return null;
}
