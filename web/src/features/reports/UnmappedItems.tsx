import { Loading } from '@registry/components/Loading';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { useLookup } from '@/shared/useLookup';
import { useReplaceSourceItems, useUnmappedItems } from './editorApi';
import { rowItems } from './itemMappings';
import type { EditorRow, EditorSource, EditorView } from './types';

export interface UnmappedItemsProps {
  view: EditorView;
  /** The row selected in the tree, which an unmapped item is assigned to when it is a data row. */
  row: EditorRow | null;
}

/**
 * The items of each bridge source that map to no row, which the report leaves out: the members of the source's item dimension,
 * from its published version, that take source values but no row takes. Each is assigned to the selected data row.
 */
export function UnmappedItems({ view, row }: UnmappedItemsProps) {
  const bridgeSources = view.sources.filter(source => source.source.mappingMethod === 'bridge');
  if (bridgeSources.length === 0) {
    return null;
  }
  return (
    <div data-slot="unmapped-items" className="space-y-3">
      <h3 className="text-sm font-medium">Unmapped items</h3>
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

/** One bridge source's unmapped items, with their labels from the item dimension's published version. */
function UnmappedSection({ view, source, row }: UnmappedSectionProps) {
  const sourceId = source.source.reportSourceId;
  const unmapped = useUnmappedItems(view.report.reportId, sourceId);
  const members = useLookup('members', source.itemDimensionVersionId ?? null);
  const assign = useReplaceSourceItems(view.report.reportId);
  const labels = new Map(members.data?.map(member => [member.key, member.label]));

  return (
    <section data-slot="unmapped-section" className="space-y-2 rounded-md border p-3 text-sm">
      <div className="flex items-center gap-2">
        <span className="font-medium">{source.source.displayName}</span>
        {unmapped.data && <Badge variant={unmapped.data.length === 0 ? 'secondary' : 'destructive'}>{unmapped.data.length} unmapped</Badge>}
      </div>
      <ApiErrorAlert error={unmapped.error ?? assign.error} />
      {unmapped.isPending && <Loading size="sm" />}
      {unmapped.data?.length === 0 && <p className="text-muted-foreground">Every item maps to a row.</p>}
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
                onClick={() => assign.mutate({ sourceId, rowKey: row.key, items: [...rowItems(row, sourceId), { itemKey, factor: 1 }] })}
              >
                Assign to {row.label}
              </Button>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}
