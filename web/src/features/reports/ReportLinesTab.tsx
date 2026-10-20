import type { UniqueIdentifier } from '@dnd-kit/core';
import { Plus } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';
import { Loading } from '@registry/components/Loading';
import { Nestable, type NestableItemsChange, type TreeItem, type TreeItems } from '@registry/components/Nestable';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { useLookup } from '@/shared/useLookup';
import { useReplaceReportLines, useReportLines } from './api';
import { buildLineTree, toLineInputs, withLine, withLineValues, withoutLine } from './lineTree';
import { ReportLineRow } from './ReportLineRow';
import { ReportLineSheet } from './ReportLineSheet';
import type { ReportLineInput, ReportVersion } from './types';

export interface ReportLinesTabProps {
  version: ReportVersion;
  isEditable: boolean;
}

/** The version's line tree, once its lines and its columns have loaded. */
export function ReportLinesTab({ version, isEditable }: ReportLinesTabProps) {
  const lines = useReportLines(version.reportVersionId);
  const columns = useLookup('report-columns', version.reportVersionId);

  if (lines.isPending || columns.isPending) {
    return <Loading size="sm" />;
  }
  if (lines.error ?? columns.error) {
    return <ApiErrorAlert error={lines.error ?? columns.error} />;
  }
  const saved = buildLineTree(lines.data, new Map(columns.data.map(item => [item.id, item.key])));
  return <LineEditor key={lines.dataUpdatedAt} version={version} isEditable={isEditable} saved={saved} />;
}

/** What the line sheet was opened for: an existing line, or a new one under a parent (null for a root line). */
interface LineSheetRequest {
  item: TreeItem<ReportLineInput> | null;
  parentId: UniqueIdentifier | null;
}

interface LineEditorProps extends ReportLinesTabProps {
  /** The stored tree. */
  saved: TreeItems<ReportLineInput>;
}

/**
 * The version's lines as a tree. A draft or the published version reparents and reorders lines by drag and drop and adds,
 * edits and deletes them
 * locally; "Save" replaces the whole tree, sent in pre-order. Deleting a line hands its children to its parent.
 */
function LineEditor({ version, isEditable, saved }: LineEditorProps) {
  const replace = useReplaceReportLines(version.reportVersionId);
  const [tree, setTree] = useState(saved);
  const [sheet, setSheet] = useState<LineSheetRequest | null>(null);
  const isChanged = JSON.stringify(toLineInputs(tree)) !== JSON.stringify(toLineInputs(saved));

  function handleItemsChange(nextItems: TreeItems<ReportLineInput>, details: NestableItemsChange<ReportLineInput>) {
    if (details.source === 'drag' || details.source === 'collapse') {
      setTree(nextItems);
    }
  }

  function saveLine(request: LineSheetRequest, line: ReportLineInput) {
    setTree(current =>
      request.item === null
        ? withLine(current, request.parentId, { id: crypto.randomUUID(), data: line, children: [] })
        : withLineValues(current, request.item.id, line)
    );
  }

  return (
    <div data-slot="report-lines-tab" className="space-y-3">
      {isEditable && (
        <div className="flex flex-wrap items-center gap-2">
          <Button size="sm" onClick={() => setSheet({ item: null, parentId: null })}>
            <Plus />
            New line
          </Button>
          <div className="ml-auto flex gap-2">
            <Button variant="outline" size="sm" disabled={!isChanged || replace.isPending} onClick={() => setTree(saved)}>
              Discard changes
            </Button>
            <Button
              size="sm"
              disabled={!isChanged || replace.isPending}
              onClick={() => replace.mutate(toLineInputs(tree), { onSuccess: () => toast.success('Lines saved.') })}
            >
              Save
            </Button>
          </div>
        </div>
      )}
      <ApiErrorAlert error={replace.error} />
      {tree.length === 0 ? (
        <p className="py-6 text-center text-sm text-muted-foreground">No lines yet.</p>
      ) : (
        <div data-slot="report-line-tree" className="rounded-md border p-2">
          <Nestable
            items={tree}
            onItemsChange={handleItemsChange}
            collapsible
            handle
            enablePointer={isEditable && !replace.isPending}
            enableTouch={isEditable && !replace.isPending}
            enableKeyboard={isEditable && !replace.isPending}
            indentationWidth={24}
            renderItem={row => (
              <ReportLineRow
                {...row}
                isEditable={isEditable}
                canDrag={isEditable && !replace.isPending}
                onOpen={() => setSheet({ item: row.item, parentId: null })}
                onAddChild={() => setSheet({ item: null, parentId: row.item.id })}
                onDelete={() => setTree(current => withoutLine(current, row.item.id))}
              />
            )}
          />
        </div>
      )}
      {sheet && (
        <ReportLineSheet
          version={version}
          line={sheet.item?.data ?? null}
          readOnly={!isEditable}
          onSave={line => saveLine(sheet, line)}
          onClose={() => setSheet(null)}
        />
      )}
    </div>
  );
}
