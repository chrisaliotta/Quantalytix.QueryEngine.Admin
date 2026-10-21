import { FlipVertical2, ListChecks, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { useLookup } from '@/shared/useLookup';
import { useReplaceSourceItems } from './editorApi';
import { ItemPickerSheet } from './ItemPickerSheet';
import { sourceSections, type SourceSection } from './itemMappings';
import type { EditorRow, EditorView, SourceItemInput } from './types';

/** How a factor shows: + and − for the usual signs, the factor itself otherwise. */
function signText(factor: number) {
  if (factor === 1) {
    return '+';
  }
  return factor === -1 ? '−' : String(factor);
}

export interface RowSourcesProps {
  view: EditorView;
  /** The row selected in the tree; null before one is. */
  row: EditorRow | null;
}

/**
 * What each source gives the selected data row, one section per source: a bridge source's items, whose signs flip and which are
 * removed or added here, and a direct source's field, whose rows with the row's key are the row's.
 */
export function RowSources({ view, row }: RowSourcesProps) {
  if (row === null) {
    return <p className="py-6 text-center text-sm text-muted-foreground">Select a row to see what each source gives it.</p>;
  }
  if (row.kind !== 'data') {
    return (
      <p className="py-6 text-center text-sm text-muted-foreground">
        {row.kind === 'group' ? 'A group totals the rows under it' : 'Sections and formula rows show no source values'}; only data
        rows take source rows.
      </p>
    );
  }
  if (view.sources.length === 0) {
    return (
      <p className="py-6 text-center text-sm text-muted-foreground">
        No sources yet. Add one in the Sources tab; its rows reach this row by bridge or by field.
      </p>
    );
  }
  return (
    <div data-slot="row-sources" className="space-y-3">
      {sourceSections(view, row).map(section => (
        <SourceSectionCard key={section.source.source.reportSourceId} view={view} row={row} section={section} />
      ))}
    </div>
  );
}

interface SourceSectionCardProps {
  view: EditorView;
  row: EditorRow;
  section: SourceSection;
}

/** One source's section of a data row. */
function SourceSectionCard({ view, row, section }: SourceSectionCardProps) {
  const { source } = section;
  return (
    <section data-slot="row-source-section" className="space-y-2 rounded-md border p-3 text-sm">
      <div className="flex items-center gap-2">
        <span className="font-medium">{source.source.displayName}</span>
        <span className="font-mono text-xs text-muted-foreground">{source.datasetKey}</span>
        <Badge variant="outline">{section.method === 'bridge' ? `Bridge from ${source.roleKey}` : 'Direct'}</Badge>
      </div>
      {section.method === 'bridge' ? (
        <BridgeItems view={view} row={row} section={section} />
      ) : (
        <p className="text-muted-foreground">
          Rows whose <code className="text-foreground">{section.fieldKey}</code> is <code className="text-foreground">{section.rowKey}</code>.
        </p>
      )}
    </section>
  );
}

interface BridgeItemsProps {
  view: EditorView;
  row: EditorRow;
  section: Extract<SourceSection, { method: 'bridge' }>;
}

/** A bridge source's items on the row, with their labels and signs; each sign flips, each item is removed, and items are added. */
function BridgeItems({ view, row, section }: BridgeItemsProps) {
  const replace = useReplaceSourceItems(view.report.reportId);
  const members = useLookup('members', section.source.itemDimensionVersionId ?? null);
  const [picking, setPicking] = useState(false);
  const labels = new Map(members.data?.map(member => [member.key, member.label]));
  const sourceId = section.source.source.reportSourceId;

  function save(items: SourceItemInput[]) {
    replace.mutate({ sourceId, rowKey: row.key, items });
  }

  return (
    <div className="space-y-2">
      {section.items.length === 0 && <p className="text-muted-foreground">No items yet; the source adds nothing to the row.</p>}
      <ul className="divide-y">
        {section.items.map(item => (
          <li key={item.itemKey} className="flex items-center gap-2 py-1">
            <span className="w-4 text-center font-mono">{signText(item.factor)}</span>
            <span className="font-mono text-xs">{item.itemKey}</span>
            <span className="truncate text-muted-foreground">{labels.get(item.itemKey)}</span>
            <div className="ml-auto flex items-center">
              <Button
                variant="ghost"
                size="icon-xs"
                aria-label={`Flip the sign of ${item.itemKey}`}
                disabled={replace.isPending}
                onClick={() => save(section.items.map(other => (other === item ? { ...other, factor: -other.factor } : other)))}
              >
                <FlipVertical2 />
              </Button>
              <Button
                variant="ghost"
                size="icon-xs"
                aria-label={`Remove ${item.itemKey}`}
                disabled={replace.isPending}
                onClick={() => save(section.items.filter(other => other !== item))}
              >
                <Trash2 />
              </Button>
            </div>
          </li>
        ))}
      </ul>
      <Button variant="outline" size="sm" onClick={() => setPicking(true)}>
        <ListChecks />
        Add items
      </Button>
      <ApiErrorAlert error={replace.error} />
      {picking && <ItemPickerSheet view={view} source={section.source} row={row} onClose={() => setPicking(false)} />}
    </div>
  );
}
