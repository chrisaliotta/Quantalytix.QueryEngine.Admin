import { ChevronDown, Eye, GripVertical, Pencil, Plus, Trash2 } from 'lucide-react';
import type { NestableRenderItemProps } from '@registry/components/Nestable';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { DimensionMember } from './types';

/** The Nestable render props of the row, plus what the row shows and does. */
export interface MemberTreeRowProps extends NestableRenderItemProps<DimensionMember> {
  /** Level keys by level id, for the level badge. */
  levelKeys: ReadonlyMap<number, string>;
  isEditable: boolean;
  canDrag: boolean;
  onOpen: (member: DimensionMember) => void;
  onAddChild: (member: DimensionMember) => void;
  onDelete: (member: DimensionMember) => void;
}

/**
 * One member of the Nestable tree: collapse toggle, drag handle (editable versions only), key, label, level and flags, and its
 * actions. A member shown away from its stored parent (a foreign parent or a cycle) is flagged as detached.
 */
export function MemberTreeRow({
  item,
  depth,
  indentationWidth,
  style,
  isDragging,
  collapsed,
  childCount,
  onCollapse,
  setNodeRef,
  setActivatorNodeRef,
  attributes,
  activatorListeners,
  levelKeys,
  isEditable,
  canDrag,
  onOpen,
  onAddChild,
  onDelete,
}: MemberTreeRowProps) {
  const member = item.data as DimensionMember;
  const level = member.levelId == null ? undefined : levelKeys.get(member.levelId);
  const shownParentId = item.parentId === null ? null : Number(item.parentId);
  const isDetached = (member.parentMemberId ?? null) !== shownParentId;

  return (
    <div
      ref={setNodeRef}
      data-slot="member-tree-row"
      style={{ ...style, paddingLeft: depth * indentationWidth }}
      className={cn('group flex items-center gap-1 rounded-md px-1 py-1 text-sm hover:bg-accent/50', isDragging && 'opacity-60')}
    >
      <CollapseToggle collapsed={collapsed} onCollapse={onCollapse} />
      {canDrag && (
        <button
          type="button"
          ref={setActivatorNodeRef}
          data-slot="member-tree-handle"
          aria-label={`Drag ${member.memberKey}`}
          className="flex size-6 cursor-grab items-center justify-center rounded-sm text-muted-foreground hover:text-foreground"
          {...attributes}
          {...activatorListeners}
        >
          <GripVertical className="size-4" />
        </button>
      )}
      <button
        type="button"
        data-slot="member-tree-label"
        className="flex min-w-0 flex-1 items-center gap-2 text-left"
        onClick={() => onOpen(member)}
      >
        <span className="font-mono text-xs text-muted-foreground">{member.memberKey}</span>
        <span className="truncate">{member.displayName}</span>
        {level && <Badge variant="outline">{level}</Badge>}
        {!member.isAssignable && <Badge variant="secondary">Not assignable</Badge>}
        {isDetached && <Badge variant="destructive">Detached</Badge>}
        {childCount > 0 && <span className="text-xs text-muted-foreground">{childCount}</span>}
      </button>
      <div data-slot="member-tree-actions" className="flex items-center opacity-0 group-hover:opacity-100 focus-within:opacity-100">
        <Button type="button" variant="ghost" size="icon-xs" aria-label={`Open ${member.memberKey}`} onClick={() => onOpen(member)}>
          <OpenIcon isEditable={isEditable} />
        </Button>
        {isEditable && (
          <>
            <Button type="button" variant="ghost" size="icon-xs" aria-label={`Add a child to ${member.memberKey}`} onClick={() => onAddChild(member)}>
              <Plus />
            </Button>
            <Button type="button" variant="ghost" size="icon-xs" aria-label={`Delete ${member.memberKey}`} onClick={() => onDelete(member)}>
              <Trash2 />
            </Button>
          </>
        )}
      </div>
    </div>
  );
}

function CollapseToggle({ collapsed, onCollapse }: { collapsed: boolean; onCollapse?: () => void }) {
  if (!onCollapse) {
    return <span className="size-6" />;
  }
  return (
    <button
      type="button"
      data-slot="member-tree-collapse"
      aria-label={collapsed ? 'Expand' : 'Collapse'}
      className="flex size-6 items-center justify-center rounded-sm text-muted-foreground hover:text-foreground"
      onClick={onCollapse}
    >
      <ChevronDown className={cn('size-4 transition-transform', collapsed && '-rotate-90')} />
    </button>
  );
}

function OpenIcon({ isEditable }: { isEditable: boolean }) {
  if (isEditable) {
    return <Pencil />;
  }
  return <Eye />;
}
