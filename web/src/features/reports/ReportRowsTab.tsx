import { Plus } from 'lucide-react';
import { useState } from 'react';
import { Loading } from '@registry/components/Loading';
import { Nestable, type NestableItemsChange, type TreeItems } from '@registry/components/Nestable';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import type { LookupItem } from '@/shared/types';
import { usePublishedVersion } from '@/shared/useLatestVersionItems';
import { useLookup } from '@/shared/useLookup';
import {
  useReplaceReportLines,
  useReplaceReportMemberOptions,
  useReportDimensionMembers,
  useReportLines,
  useReportMemberOptions,
} from './api';
import { ReportMemberOptionSheet } from './ReportMemberOptionSheet';
import { ReportRowItem } from './ReportRowItem';
import { ReportRowSheet } from './ReportRowSheet';
import {
  buildRowTree,
  nextSortOrder,
  selfAndBelow,
  toLineInput,
  toMemberOptionInput,
  withMemberOption,
  withOverlayRow,
  withoutOverlayRow,
  type ReportRowNode,
} from './rowTree';
import type { Report, ReportDimensionMember, ReportLineInput, ReportMemberOptionInput, ReportVersion } from './types';

export interface ReportRowsTabProps {
  report: Report;
  version: ReportVersion;
  isEditable: boolean;
}

/**
 * The version's rows, once the members of the report dimension's published version, the version's overlay rows, member
 * options and columns have loaded. Requests lay the rows out from the dimension version their assumption set pins, by
 * default the published one.
 */
export function ReportRowsTab({ report, version, isEditable }: ReportRowsTabProps) {
  const dimensionVersion = usePublishedVersion('dimension-versions', report.reportDimensionId);
  const members = useReportDimensionMembers(dimensionVersion?.id ?? null);
  const lines = useReportLines(version.reportVersionId);
  const options = useReportMemberOptions(version.reportVersionId);
  const columns = useLookup('report-columns', version.reportVersionId);

  if (dimensionVersion === null) {
    return (
      <p className="py-6 text-center text-sm text-muted-foreground">
        The report dimension has no published version yet; its members are the report's rows.
      </p>
    );
  }
  const error = members.error ?? lines.error ?? options.error ?? columns.error;
  if (error) {
    return <ApiErrorAlert error={error} />;
  }
  if (!members.data || !lines.data || !options.data || !columns.data) {
    return <Loading size="sm" />;
  }
  const columnKeys = new Map(columns.data.map(item => [item.id, item.key]));
  return (
    <RowEditor
      version={version}
      isEditable={isEditable}
      members={members.data}
      lines={lines.data.map(line => toLineInput(line, columnKeys))}
      options={options.data.map(toMemberOptionInput)}
    />
  );
}

/** What a sheet was opened for: an overlay row, or a new one under a parent (null for the top), or a member's option. */
type RowSheetRequest = { line: ReportLineInput | null; parentKey: string | null } | { member: ReportRowNode };

interface RowEditorProps {
  version: ReportVersion;
  isEditable: boolean;
  members: ReportDimensionMember[];
  /** The stored overlay rows, as the replace body takes them. */
  lines: ReportLineInput[];
  /** The stored member options, as the replace body takes them. */
  options: ReportMemberOptionInput[];
}

/**
 * The rows as a tree in the engine's order: the report dimension's members with their options, and the overlay rows among
 * them. A draft or the published version adds, edits and deletes overlay rows and edits member options in sheets, each
 * saved as a replace of the whole list; deleting an overlay row hands the rows under it to its parent.
 */
function RowEditor({ version, isEditable, members, lines, options }: RowEditorProps) {
  const saveLines = useReplaceReportLines(version.reportVersionId);
  const removeLine = useReplaceReportLines(version.reportVersionId);
  const saveOptions = useReplaceReportMemberOptions(version.reportVersionId);
  const [collapsedKeys, setCollapsedKeys] = useState<ReadonlySet<string>>(() => new Set());
  const [sheet, setSheet] = useState<RowSheetRequest | null>(null);
  const [deleting, setDeleting] = useState<ReportLineInput | null>(null);
  const tree = buildRowTree(members, lines, options, collapsedKeys);

  function handleItemsChange(_items: TreeItems<ReportRowNode>, details: NestableItemsChange<ReportRowNode>) {
    if (details.source === 'collapse') {
      setCollapsedKeys(toggled(collapsedKeys, String(details.activeId)));
    }
  }

  function open(row: ReportRowNode) {
    setSheet(row.line ? { line: row.line, parentKey: null } : { member: row });
  }

  return (
    <div data-slot="report-rows-tab" className="space-y-3">
      {isEditable && (
        <Button size="sm" onClick={() => setSheet({ line: null, parentKey: null })}>
          <Plus />
          New row
        </Button>
      )}
      <ApiErrorAlert error={removeLine.error} />
      {tree.length === 0 ? (
        <p className="py-6 text-center text-sm text-muted-foreground">The report dimension's version has no members yet.</p>
      ) : (
        <div data-slot="report-row-tree" className="rounded-md border p-2">
          <Nestable
            items={tree}
            onItemsChange={handleItemsChange}
            collapsible
            enablePointer={false}
            enableTouch={false}
            enableKeyboard={false}
            indentationWidth={24}
            renderItem={row => (
              <ReportRowItem
                {...row}
                isEditable={isEditable}
                onOpen={() => open(row.item.data as ReportRowNode)}
                onAddChild={() => setSheet({ line: null, parentKey: String(row.item.id) })}
                onDelete={() => setDeleting((row.item.data as ReportRowNode).line)}
              />
            )}
          />
        </div>
      )}
      {sheet && 'member' in sheet && (
        <ReportMemberOptionSheet
          row={sheet.member}
          readOnly={!isEditable}
          onSave={option => saveOptions.mutateAsync(withMemberOption(options, option))}
          onClose={() => setSheet(null)}
        />
      )}
      {sheet && 'line' in sheet && (
        <ReportRowSheet
          version={version}
          line={sheet.line}
          placement={{ parentKey: sheet.parentKey, sortOrder: nextSortOrder(tree, sheet.parentKey) }}
          parents={parentItems(members, lines, sheet.line === null ? [] : selfAndBelow(lines, sheet.line.lineKey))}
          readOnly={!isEditable}
          onSave={line => saveLines.mutateAsync(withOverlayRow(lines, sheet.line?.lineKey ?? null, line))}
          onClose={() => setSheet(null)}
        />
      )}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => !open && setDeleting(null)}
        title={`Delete row ${deleting?.lineKey ?? ''}?`}
        description="The rows under it move up to its parent."
        confirmLabel="Delete row"
        destructive
        onConfirm={() => deleting && removeLine.mutate(withoutOverlayRow(lines, deleting.lineKey))}
      />
    </div>
  );
}

/** The rows an overlay row can sit under: every member, and every overlay row but the excluded ones. */
function parentItems(members: ReportDimensionMember[], lines: ReportLineInput[], excluded: string[]): LookupItem[] {
  return [
    ...members.map(member => ({ id: member.memberId, key: member.memberKey, label: member.displayName })),
    ...lines
      .filter(line => !excluded.includes(line.lineKey))
      .map(line => ({ id: line.reportLineId ?? 0, key: line.lineKey, label: line.displayName })),
  ];
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
