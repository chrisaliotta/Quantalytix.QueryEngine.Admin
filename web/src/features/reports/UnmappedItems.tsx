import { Loading } from '@registry/components/Loading';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { useDimensionVersionMembers } from './api';
import { useReplaceSourceItems, useUnmappedItems } from './editorApi';
import { factorText, itemReach, rowItems, unevenItems } from './itemMappings';
import type { EditorRow, EditorSource, EditorView } from './types';

export interface UnmappedItemsProps {
  view: EditorView;
  /** The row selected in the tree, which an unmapped item is assigned to when it is a data row. */
  row: EditorRow | null;
}

/**
 * How each bridge source's items reach the report: the items no rule reaches, which the report leaves out and which can each be
 * assigned to the selected data row, and the items whose rules' factors add up to other than 1, which the report's total counts
 * other than once, such as an item added whole into two rows.
 */
export function UnmappedItems({ view, row }: UnmappedItemsProps) {
  const bridgeSources = view.sources.filter(source => source.source.mappingMethod === 'bridge');
  if (bridgeSources.length === 0) {
    return null;
  }
  return (
    <div data-slot="unmapped-items" className="space-y-3">
      <h3 className="text-sm font-medium">Item coverage</h3>
      {bridgeSources.map(source => (
        <UnmappedSection key={source.source.reportSourceId} view={view} source={source} row={row?.kind === 'data' ? row : null} />
      ))}
    </div>
  );
}

interface UnmappedSectionProps {
  view: EditorView;
  source: EditorSource;
  /** The selected data row, or null when no data row is selected. */
  row: EditorRow | null;
}

/** One bridge source's unmapped and unevenly counted items, with their labels from the item dimension's published version. */
function UnmappedSection({ view, source, row }: UnmappedSectionProps) {
  const sourceId = source.source.reportSourceId;
  const unmapped = useUnmappedItems(view.report.reportId, sourceId);
  const members = useDimensionVersionMembers(source.itemDimensionVersionId ?? null);
  const assign = useReplaceSourceItems(view.report.reportId);
  const labels = new Map(members.data?.map(member => [member.memberKey, member.displayName]));
  const uneven = members.data ? unevenItems(itemReach(view.rows, sourceId, members.data)) : [];

  return (
    <section data-slot="unmapped-section" className="space-y-2 rounded-md border p-3 text-sm">
      <div className="flex items-center gap-2">
        <span className="font-medium">{source.source.displayName}</span>
        {unmapped.data && <Badge variant={unmapped.data.length === 0 ? 'secondary' : 'destructive'}>{unmapped.data.length} unmapped</Badge>}
        {uneven.length > 0 && <Badge variant="outline">{uneven.length} counted other than once</Badge>}
      </div>
      <ApiErrorAlert error={unmapped.error ?? members.error ?? assign.error} />
      {unmapped.isPending && <Loading size="sm" />}
      {unmapped.data?.length === 0 && <p className="text-muted-foreground">Every item adds into a row.</p>}
      <ul className="divide-y">
        {unmapped.data?.map(itemKey => (
          <li key={itemKey} className="flex items-center gap-2 py-1">
            <span className="font-mono text-xs">{itemKey}</span>
            <span className="truncate text-muted-foreground">{labels.get(itemKey)}</span>
            {row && (
              <Button
                variant="outline"
                size="xs"
                className="ml-auto"
                disabled={assign.isPending}
                onClick={() =>
                  assign.mutate({ sourceId, rowKey: row.key, items: [...rowItems(row, sourceId), { itemKey, selection: 'member', factor: 1 }] })
                }
              >
                Assign to {row.label}
              </Button>
            )}
          </li>
        ))}
      </ul>
      {uneven.length > 0 && (
        <ul className="divide-y border-t">
          {uneven.map(({ itemKey, rules, weight }) => (
            <li key={itemKey} className="flex items-center gap-2 py-1">
              <span className="font-mono text-xs">{itemKey}</span>
              <span className="truncate text-muted-foreground">{labels.get(itemKey)}</span>
              <span className="ml-auto truncate text-xs text-muted-foreground">
                {rules.map(rule => `${rule.row.label} ${factorText(rule.factor)}${rule.folder ? ` in ${rule.folder}` : ''}`).join(', ')}
              </span>
              <Badge variant="outline" className="font-mono">
                total {factorText(weight)}
              </Badge>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
