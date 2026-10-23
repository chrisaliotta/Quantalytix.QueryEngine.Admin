import { Plus, Search } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Loading } from '@registry/components/Loading';
import { Nestable, type NestableItemsChange, type TreeItems } from '@registry/components/Nestable';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { useDeleteMember, useLevels, useMembers, useReorderMembers } from './api';
import { MemberSheet } from './MemberSheet';
import { applyCollapsed, buildMemberTree, filterMemberTree, placementsAfterMove } from './memberTree';
import { MemberTreeRow } from './MemberTreeRow';
import type { Dimension, DimensionMember } from './types';

/** What the member sheet was opened for: an existing member, or a new one under a parent. */
interface MemberSheetRequest {
  member: DimensionMember | null;
  parentId: number | null;
}

export interface MembersTabProps {
  dimension: Dimension;
  versionId: number;
  isEditable: boolean;
}

/**
 * The version's member tree. A draft or the published version reparents and reorders members by drag and drop, saved in
 * one call; search narrows the tree to matches and their ancestors (dragging pauses while searching). Members open in a
 * sheet.
 */
export function MembersTab({ dimension, versionId, isEditable }: MembersTabProps) {
  const members = useMembers(versionId);
  const levels = useLevels(versionId);
  const reorder = useReorderMembers(versionId);
  const deleteMember = useDeleteMember(versionId);
  const [search, setSearch] = useState('');
  const [collapsedIds, setCollapsedIds] = useState<ReadonlySet<number>>(() => new Set());
  const [pendingTree, setPendingTree] = useState<TreeItems<DimensionMember> | null>(null);
  const [sheet, setSheet] = useState<MemberSheetRequest | null>(null);
  const [deleting, setDeleting] = useState<DimensionMember | null>(null);

  const rows = members.data ?? [];
  const levelKeys = new Map((levels.data ?? []).map(level => [level.dimensionLevelId, level.levelKey]));
  const tree = pendingTree ?? applyCollapsed(buildMemberTree(rows), collapsedIds);
  const visible = filterMemberTree(tree, search);
  const canDrag = isEditable && search.trim() === '' && !reorder.isPending;

  function handleItemsChange(nextItems: TreeItems<DimensionMember>, details: NestableItemsChange<DimensionMember>) {
    if (details.source === 'collapse') {
      setCollapsedIds(toggled(collapsedIds, Number(details.activeId)));
      return;
    }
    if (details.source !== 'drag') {
      return;
    }
    const placements = placementsAfterMove(rows, nextItems, Number(details.activeId));
    if (placements.length === 0) {
      return;
    }
    setPendingTree(nextItems);
    reorder.mutate(placements, { onSettled: () => setPendingTree(null) });
  }

  return (
    <div data-slot="members-tab" className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <div className="relative w-full max-w-xs">
          <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input className="pl-8" placeholder="Search members..." value={search} onChange={event => setSearch(event.target.value)} />
        </div>
        <span className="text-sm text-muted-foreground">{rows.length} members</span>
        {isEditable && (
          <Button size="sm" className="ml-auto" onClick={() => setSheet({ member: null, parentId: null })}>
            <Plus />
            New member
          </Button>
        )}
      </div>
      {dimension.dimensionKind === 'time' && (
        <p className="text-sm text-muted-foreground">
          Time dimensions derive their periods from dates, so members are needed only for custom calendars.
        </p>
      )}
      <ApiErrorAlert error={reorder.error} />
      <MemberTreeBody isLoading={members.isPending} isEmpty={visible.length === 0} search={search}>
        <Nestable
          items={visible}
          onItemsChange={handleItemsChange}
          collapsible
          handle
          enablePointer={canDrag}
          enableTouch={canDrag}
          enableKeyboard={canDrag}
          indentationWidth={24}
          renderItem={row => (
            <MemberTreeRow
              {...row}
              levelKeys={levelKeys}
              isEditable={isEditable}
              canDrag={canDrag}
              onOpen={member => setSheet({ member, parentId: null })}
              onAddChild={member => setSheet({ member: null, parentId: member.dimensionMemberId })}
              onDelete={setDeleting}
            />
          )}
        />
      </MemberTreeBody>

      {sheet && (
        <MemberSheet
          versionId={versionId}
          members={rows}
          member={sheet.member}
          parentId={sheet.parentId}
          readOnly={!isEditable}
          onClose={() => setSheet(null)}
        />
      )}
      <ApiErrorAlert error={deleteMember.error} />
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => !open && setDeleting(null)}
        title={`Delete member ${deleting?.memberKey ?? ''}?`}
        description="Members with children, mapping entries, range bands or bridge mappings cannot be deleted."
        confirmLabel="Delete member"
        destructive
        onConfirm={() => deleting && deleteMember.mutate(deleting.dimensionMemberId)}
      />
    </div>
  );
}

function MemberTreeBody(props: { isLoading: boolean; isEmpty: boolean; search: string; children: ReactNode }) {
  if (props.isLoading) {
    return <Loading size="sm" />;
  }
  if (props.isEmpty && props.search.trim()) {
    return <p className="py-6 text-center text-sm text-muted-foreground">No members match "{props.search.trim()}".</p>;
  }
  if (props.isEmpty) {
    return <p className="py-6 text-center text-sm text-muted-foreground">No members yet.</p>;
  }
  return <div data-slot="member-tree" className="rounded-md border p-2">{props.children}</div>;
}

function toggled(ids: ReadonlySet<number>, id: number): ReadonlySet<number> {
  const next = new Set(ids);
  if (next.has(id)) {
    next.delete(id);
  } else {
    next.add(id);
  }
  return next;
}
